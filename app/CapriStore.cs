using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace CapriDesktop
{
    // SQLite supplied by Windows 10+, with no downloaded native library.
    // API contracts: sqlite.org/c3ref/prepare.html, bind_blob.html and backup_finish.html.
    internal sealed class CapriStore : IDisposable
    {
        internal const int SchemaVersion = 1;
        private const int ApplicationId = 1128353874;
        private static readonly object pathsGate = new object();
        private static readonly Dictionary<string, object> pathLocks = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly object gate;
        private IntPtr database;
        internal string DbPath { get; private set; }
        internal bool IsDisposed { get { lock (gate) return database == IntPtr.Zero; } }

        internal CapriStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A database path is required.", "path");
            DbPath = Path.GetFullPath(path);
            lock (pathsGate)
            {
                if (!pathLocks.TryGetValue(DbPath, out gate)) { gate = new object(); pathLocks.Add(DbPath, gate); }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(DbPath));
            lock (gate)
            {
                try
                {
                    Check(Native.sqlite3_open_v2(Utf8(DbPath), out database, 2 | 4 | 0x10000, IntPtr.Zero));
                    Check(Native.sqlite3_busy_timeout(database, 5000));
                    Sql connection = new Sql(database);
                    connection.Execute("PRAGMA foreign_keys=ON");
                    connection.Execute("PRAGMA synchronous=FULL");
                    connection.Query("PRAGMA journal_mode=WAL");
                    connection.Query("PRAGMA secure_delete=ON");
                    Write(delegate(Sql sql)
                    {
                        int application = int.Parse(sql.Scalar("PRAGMA application_id") ?? "0", CultureInfo.InvariantCulture);
                        int version = int.Parse(sql.Scalar("PRAGMA user_version") ?? "0", CultureInfo.InvariantCulture);
                        if (application != 0 && application != ApplicationId) throw new InvalidDataException("This database belongs to another application.");
                        if (version > SchemaVersion) throw new InvalidDataException("This database needs a newer Capri version.");
                        if (version == 0)
                        {
                            if (application == 0 && sql.Scalar("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'") != "0") throw new InvalidDataException("An unrelated database cannot be adopted.");
                            sql.Execute("CREATE TABLE settings(key TEXT PRIMARY KEY NOT NULL,value TEXT NOT NULL)");
                            sql.Execute("CREATE TABLE memories(id TEXT PRIMARY KEY NOT NULL,title TEXT NOT NULL,content TEXT NOT NULL,source TEXT NOT NULL,created_utc TEXT NOT NULL,updated_utc TEXT NOT NULL)");
                            sql.Execute("CREATE INDEX memories_updated ON memories(updated_utc DESC,id)");
                            sql.Execute("CREATE TABLE chronicle(seq INTEGER PRIMARY KEY NOT NULL,at_utc TEXT NOT NULL,kind TEXT NOT NULL,scope TEXT NOT NULL,operation_hash TEXT NOT NULL,outcome TEXT NOT NULL,previous_hash TEXT NOT NULL,entry_hash TEXT NOT NULL)");
                            sql.Execute("CREATE TRIGGER chronicle_no_update BEFORE UPDATE ON chronicle BEGIN SELECT RAISE(ABORT,'Chronicle is append-only'); END");
                            sql.Execute("CREATE TRIGGER chronicle_no_delete BEFORE DELETE ON chronicle BEGIN SELECT RAISE(ABORT,'Chronicle is append-only'); END");
                            sql.Execute("PRAGMA application_id=" + ApplicationId.ToString(CultureInfo.InvariantCulture));
                            sql.Execute("PRAGMA user_version=" + SchemaVersion.ToString(CultureInfo.InvariantCulture));
                        }
                        else if (application != ApplicationId) throw new InvalidDataException("The database identity is invalid.");
                        return 0;
                    });
                }
                catch { Close(); throw; }
            }
        }

        internal T Read<T>(Func<Sql, T> read)
        {
            if (read == null) throw new ArgumentNullException("read");
            lock (gate) { EnsureOpen(); return read(new Sql(database)); }
        }

        internal T Write<T>(Func<Sql, T> write)
        {
            if (write == null) throw new ArgumentNullException("write");
            lock (gate)
            {
                EnsureOpen();
                Sql sql = new Sql(database);
                sql.Execute("BEGIN IMMEDIATE");
                try { T result = write(sql); sql.Execute("COMMIT"); return result; }
                catch { try { sql.Execute("ROLLBACK"); } catch { } throw; }
            }
        }

        internal string GetSetting(string key)
        {
            return Read(delegate(Sql sql) { return sql.Scalar("SELECT value FROM settings WHERE key=?", key); });
        }

        internal void SetSetting(string key, string value)
        {
            Write(delegate(Sql sql) { sql.SetSetting(key, value); return 0; });
        }

        internal void Backup(string destination)
        {
            string target = ExportTarget(destination);
            string targetAnchor = target + ".chronicle-head";
            if (File.Exists(targetAnchor) || Directory.Exists(targetAnchor)) throw new IOException("The backup audit-head target already exists.");
            string temporary = target + "." + Guid.NewGuid().ToString("N") + ".partial";
            IntPtr copy = IntPtr.Zero;
            byte[] auditHead = null;
            try
            {
                using (FileStream created = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
                lock (gate)
                {
                    EnsureOpen();
                    Check(Native.sqlite3_open_v2(Utf8(temporary), out copy, 2 | 0x10000, IntPtr.Zero));
                    Check(Native.sqlite3_busy_timeout(copy, 5000));
                    IntPtr backup = Native.sqlite3_backup_init(copy, Utf8("main"), database, Utf8("main"));
                    if (backup == IntPtr.Zero) throw new IOException("SQLite could not start the backup.");
                    int status;
                    try
                    {
                        int retries = 0;
                        do
                        {
                            status = Native.sqlite3_backup_step(backup, -1);
                            if (status == 5 || status == 6) Thread.Sleep(50);
                        } while ((status == 5 || status == 6) && ++retries < 20);
                    }
                    finally { Check(Native.sqlite3_backup_finish(backup)); }
                    if (status != 101) throw new IOException("The SQLite backup did not complete.");
                    Check(Native.sqlite3_close_v2(copy));
                    copy = IntPtr.Zero;
                    string sourceAnchor = DbPath + ".chronicle-head";
                    if (File.Exists(sourceAnchor))
                    {
                        if (new FileInfo(sourceAnchor).Length > 256) throw new IOException("The audit head is invalid.");
                        auditHead = File.ReadAllBytes(sourceAnchor);
                    }
                }
                File.Move(temporary, target);
                if (auditHead != null)
                    using (FileStream head = new FileStream(targetAnchor, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { head.Write(auditHead, 0, auditHead.Length); head.Flush(true); }
            }
            finally
            {
                if (copy != IntPtr.Zero) Native.sqlite3_close_v2(copy);
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        internal void Export(string destination, Action<Stream> write)
        {
            if (write == null) throw new ArgumentNullException("write");
            string target = ExportTarget(destination);
            string temporary = target + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                using (FileStream file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { write(file); file.Flush(true); }
                File.Move(temporary, target);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private string ExportTarget(string destination)
        {
            if (string.IsNullOrWhiteSpace(destination)) throw new ArgumentException("An export path is required.", "destination");
            string target = Path.GetFullPath(destination);
            if (string.Equals(target, DbPath, StringComparison.OrdinalIgnoreCase) || target.StartsWith(DbPath + "-", StringComparison.OrdinalIgnoreCase) || string.Equals(target, DbPath + ".chronicle-head", StringComparison.OrdinalIgnoreCase)) throw new IOException("The live database cannot be an export target.");
            if (File.Exists(target) || Directory.Exists(target)) throw new IOException("The export target already exists. Choose a new file.");
            string parent = Path.GetDirectoryName(target);
            if (!Directory.Exists(parent)) throw new DirectoryNotFoundException("The export folder does not exist.");
            for (DirectoryInfo folder = new DirectoryInfo(parent); folder != null; folder = folder.Parent)
                if ((folder.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Export through a linked folder is not allowed.");
            return target;
        }

        private void EnsureOpen() { if (database == IntPtr.Zero) throw new ObjectDisposedException("CapriStore"); }
        private static void Check(int result) { if (result != 0) throw new IOException("SQLite operation failed (code " + result.ToString(CultureInfo.InvariantCulture) + ")."); }
        internal static byte[] Utf8(string value) { return Encoding.UTF8.GetBytes(value + "\0"); }

        private void Close()
        {
            IntPtr closing = database;
            database = IntPtr.Zero;
            if (closing != IntPtr.Zero) Native.sqlite3_close_v2(closing);
        }

        public void Dispose() { lock (gate) Close(); GC.SuppressFinalize(this); }
        ~CapriStore() { Close(); }

        internal sealed class Sql
        {
            private readonly IntPtr connection;
            internal Sql(IntPtr connection) { this.connection = connection; }

            internal int Execute(string command, params object[] values)
            {
                using (Statement statement = Prepare(command, values))
                {
                    int result = Native.sqlite3_step(statement.Handle);
                    if (result != 101) throw new IOException("SQLite write failed (code " + result.ToString(CultureInfo.InvariantCulture) + ").");
                    return Native.sqlite3_changes(connection);
                }
            }

            internal List<string[]> Query(string command, params object[] values)
            {
                using (Statement statement = Prepare(command, values))
                {
                    List<string[]> rows = new List<string[]>();
                    int result;
                    while ((result = Native.sqlite3_step(statement.Handle)) == 100)
                    {
                        string[] row = new string[Native.sqlite3_column_count(statement.Handle)];
                        for (int i = 0; i < row.Length; i++)
                        {
                            IntPtr pointer = Native.sqlite3_column_text(statement.Handle, i);
                            if (pointer == IntPtr.Zero) continue;
                            int length = Native.sqlite3_column_bytes(statement.Handle, i);
                            byte[] bytes = new byte[length];
                            Marshal.Copy(pointer, bytes, 0, length);
                            row[i] = Encoding.UTF8.GetString(bytes);
                        }
                        rows.Add(row);
                    }
                    if (result != 101) throw new IOException("SQLite read failed (code " + result.ToString(CultureInfo.InvariantCulture) + ").");
                    return rows;
                }
            }

            internal string Scalar(string command, params object[] values)
            {
                List<string[]> rows = Query(command, values);
                return rows.Count == 0 || rows[0].Length == 0 ? null : rows[0][0];
            }

            internal void SetSetting(string key, string value)
            {
                if (key == null || value == null) throw new ArgumentNullException();
                Execute("INSERT OR REPLACE INTO settings(key,value) VALUES(?,?)", key, value);
            }

            private Statement Prepare(string command, object[] values)
            {
                IntPtr handle;
                byte[] sql = Utf8(command);
                Check(Native.sqlite3_prepare_v2(connection, sql, sql.Length, out handle, IntPtr.Zero));
                if (handle == IntPtr.Zero) throw new IOException("An empty SQLite statement is not allowed.");
                Statement statement = new Statement(handle);
                try
                {
                    if (Native.sqlite3_bind_parameter_count(handle) != values.Length) throw new ArgumentException("SQLite parameter count does not match.");
                    for (int i = 0; i < values.Length; i++)
                    {
                        object value = values[i];
                        if (value == null) Check(Native.sqlite3_bind_null(handle, i + 1));
                        else if (value is int || value is long || value is bool) Check(Native.sqlite3_bind_int64(handle, i + 1, Convert.ToInt64(value, CultureInfo.InvariantCulture)));
                        else
                        {
                            byte[] text = Utf8(Convert.ToString(value, CultureInfo.InvariantCulture));
                            Check(Native.sqlite3_bind_text(handle, i + 1, text, text.Length - 1, new IntPtr(-1)));
                        }
                    }
                    return statement;
                }
                catch { statement.Dispose(); throw; }
            }
        }

        private sealed class Statement : IDisposable
        {
            internal IntPtr Handle;
            internal Statement(IntPtr handle) { Handle = handle; }
            public void Dispose() { if (Handle != IntPtr.Zero) { Native.sqlite3_finalize(Handle); Handle = IntPtr.Zero; } }
        }

        private static class Native
        {
            private const string Library = "winsqlite3.dll";
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)] internal static extern int sqlite3_open_v2(byte[] filename, out IntPtr db, int flags, IntPtr vfs);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)] internal static extern int sqlite3_close_v2(IntPtr db);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)] internal static extern int sqlite3_busy_timeout(IntPtr db, int milliseconds);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)] internal static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int bytes, out IntPtr statement, IntPtr tail);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)] internal static extern int sqlite3_finalize(IntPtr statement);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)] internal static extern int sqlite3_bind_parameter_count(IntPtr statement);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)] internal static extern int sqlite3_bind_null(IntPtr statement, int index);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)] internal static extern int sqlite3_bind_int64(IntPtr statement, int index, long value);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)] internal static extern int sqlite3_bind_text(IntPtr statement, int index, byte[] value, int bytes, IntPtr destructor);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)] internal static extern int sqlite3_step(IntPtr statement);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)] internal static extern int sqlite3_changes(IntPtr db);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)] internal static extern int sqlite3_column_count(IntPtr statement);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)] internal static extern IntPtr sqlite3_column_text(IntPtr statement, int index);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)] internal static extern int sqlite3_column_bytes(IntPtr statement, int index);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)] internal static extern IntPtr sqlite3_backup_init(IntPtr destination, byte[] destinationName, IntPtr source, byte[] sourceName);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)] internal static extern int sqlite3_backup_step(IntPtr backup, int pages);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.System32)] internal static extern int sqlite3_backup_finish(IntPtr backup);
        }
    }
}
