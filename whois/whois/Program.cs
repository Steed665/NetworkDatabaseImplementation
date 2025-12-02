using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace whois
{
    class Program
    {
        static bool debug = true;

        static string connStr =
            "Server=localhost;Database=AssignmentDatabase;Trusted_Connection=True;TrustServerCertificate=True;";

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Starting server on 443");
                RunServer();
            }
            else
            {
                foreach (var arg in args)
                {
                    if (debug) Console.WriteLine(arg);
                    HandleCommand(arg);
                }
            }
        }

        // DB helpers

        static string? GetUserId(string login)
        {
            try
            {
                using var conn = new SqlConnection(connStr);
                conn.Open();

                using var cmd = new SqlCommand(
                    "SELECT TOP 1 UserID FROM UserLogin WHERE LoginID=@login", conn);
                cmd.Parameters.AddWithValue("@login", login);
                return cmd.ExecuteScalar() as string;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DB error in GetUserId: {ex.Message} (check connection and LoginAccount/UserLogin)");
                return null;
            }
        }

        // Get existing user or create new one
        static string EnsureUser(string login)
        {
            var existing = GetUserId(login);
            if (!string.IsNullOrEmpty(existing)) return existing;

            try
            {
                using var conn = new SqlConnection(connStr);
                conn.Open();

                using (var cmdCheck = new SqlCommand(
                    "SELECT COUNT(*) FROM LoginAccount WHERE LoginID=@l", conn))
                {
                    cmdCheck.Parameters.AddWithValue("@l", login);
                    var count = (int)cmdCheck.ExecuteScalar();
                    if (count == 0)
                    {
                        using var cmdIns = new SqlCommand(
                            "INSERT INTO LoginAccount(LoginID) VALUES(@l)", conn);
                        cmdIns.Parameters.AddWithValue("@l", login);
                        cmdIns.ExecuteNonQuery();
                    }
                }

                string userId = Guid.NewGuid().ToString();

                using (var cmdUser = new SqlCommand(
                    "INSERT INTO CompUser(UserID,Surname,Title,LocationID) VALUES(@u,NULL,NULL,NULL)", conn))
                {
                    cmdUser.Parameters.AddWithValue("@u", userId);
                    cmdUser.ExecuteNonQuery();
                }

                using (var cmdLink = new SqlCommand(
                    "INSERT INTO UserLogin(UserID,LoginID) VALUES(@u,@l)", conn))
                {
                    cmdLink.Parameters.AddWithValue("@u", userId);
                    cmdLink.Parameters.AddWithValue("@l", login);
                    cmdLink.ExecuteNonQuery();
                }

                return userId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DB error in EnsureUser: {ex.Message} (check CompUser and UserLogin tables)");
                throw;
            }
        }

        static string GetForenames(string userId)
        {
            try
            {
                using var conn = new SqlConnection(connStr);
                conn.Open();

                using var cmd = new SqlCommand(
                    "SELECT Forename FROM UserForename WHERE UserID=@u ORDER BY ForenameOrder", conn);
                cmd.Parameters.AddWithValue("@u", userId);

                using var r = cmd.ExecuteReader();
                var list = new System.Collections.Generic.List<string>();
                while (r.Read())
                    list.Add(r["Forename"] as string ?? "");
                return string.Join(" ", list);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DB error in GetForenames: {ex.Message} (check UserForename table)");
                return "";
            }
        }

        static string GetPositions(string userId)
        {
            try
            {
                using var conn = new SqlConnection(connStr);
                conn.Open();

                using var cmd = new SqlCommand(
                    @"SELECT p.PositionName
                      FROM UserPosition up
                      JOIN Position p ON up.PositionID=p.PositionID
                      WHERE up.UserID=@u", conn);
                cmd.Parameters.AddWithValue("@u", userId);

                using var r = cmd.ExecuteReader();
                var list = new System.Collections.Generic.List<string>();
                while (r.Read())
                    list.Add(r["PositionName"] as string ?? "");
                return string.Join(", ", list);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DB error in GetPositions: {ex.Message} (check Position and UserPosition)");
                return "";
            }
        }

        static string GetList(string sql, string paramName, string paramVal)
        {
            try
            {
                using var conn = new SqlConnection(connStr);
                conn.Open();

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue(paramName, paramVal);

                using var r = cmd.ExecuteReader();
                var list = new System.Collections.Generic.List<string>();
                while (r.Read())
                    list.Add(r[0] as string ?? "");
                return string.Join(", ", list);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DB error in GetList: {ex.Message} (check related table and query)");
                return "";
            }
        }

        static int GetOrCreateId(string selectSql, string insertSql,
                                 string paramName, string paramVal)
        {
            try
            {
                using var conn = new SqlConnection(connStr);
                conn.Open();

                using (var cmdSel = new SqlCommand(selectSql, conn))
                {
                    cmdSel.Parameters.AddWithValue(paramName, paramVal);
                    var obj = cmdSel.ExecuteScalar();
                    if (obj != null && obj != DBNull.Value)
                        return Convert.ToInt32(obj);
                }

                using (var cmdIns = new SqlCommand(insertSql, conn))
                {
                    cmdIns.Parameters.AddWithValue(paramName, paramVal);
                    var obj = cmdIns.ExecuteScalar();
                    return Convert.ToInt32(obj);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DB error in GetOrCreateId: {ex.Message} (check Position or Location tables)");
                throw;
            }
        }

        // whois operations

        static void Dump(string login)
        {
            if (debug) Console.WriteLine("Dump all fields");
            string? userId = GetUserId(login);
            if (userId == null)
            {
                Console.WriteLine($"User '{login}' not known");
                return;
            }

            string? surname = null;
            string? title = null;
            string? location = null;

            try
            {
                using var conn = new SqlConnection(connStr);
                conn.Open();

                using var cmd = new SqlCommand(
                    @"SELECT c.Surname,c.Title,l.LocationDescription
                      FROM CompUser c
                      LEFT JOIN Location l ON c.LocationID=l.LocationID
                      WHERE c.UserID=@u", conn);
                cmd.Parameters.AddWithValue("@u", userId);

                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    surname = r["Surname"] as string;
                    title = r["Title"] as string;
                    location = r["LocationDescription"] as string;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DB error in Dump: {ex.Message} (check CompUser and Location)");
            }

            string forenames = GetForenames(userId);
            string positions = GetPositions(userId);
            string phones = GetList("SELECT PhoneNumber FROM UserPhone WHERE UserID=@u", "@u", userId);
            string emails = GetList("SELECT EmailAddress FROM UserEmail WHERE UserID=@u", "@u", userId);

            Console.WriteLine($"UserID={userId}");
            Console.WriteLine($"Surname={surname ?? ""}");
            Console.WriteLine($"Fornames={forenames}");
            Console.WriteLine($"Title={title ?? ""}");
            Console.WriteLine($"Position={positions}");
            Console.WriteLine($"Phone={phones}");
            Console.WriteLine($"Email={emails}");
            Console.WriteLine($"location={location ?? ""}");
        }

        static string? GetField(string login, string field)
        {
            string? userId = GetUserId(login);
            if (userId == null) return null;

            try
            {
                switch (field)
                {
                    case "UserID":
                        return userId;

                    case "Surname":
                        using (var conn = new SqlConnection(connStr))
                        {
                            conn.Open();
                            using var cmd = new SqlCommand(
                                "SELECT Surname FROM CompUser WHERE UserID=@u", conn);
                            cmd.Parameters.AddWithValue("@u", userId);
                            return cmd.ExecuteScalar() as string;
                        }

                    case "Fornames":
                        return GetForenames(userId);

                    case "Title":
                        using (var conn = new SqlConnection(connStr))
                        {
                            conn.Open();
                            using var cmd = new SqlCommand(
                                "SELECT Title FROM CompUser WHERE UserID=@u", conn);
                            cmd.Parameters.AddWithValue("@u", userId);
                            return cmd.ExecuteScalar() as string;
                        }

                    case "Position":
                        return GetPositions(userId);

                    case "Phone":
                        return GetList("SELECT PhoneNumber FROM UserPhone WHERE UserID=@u", "@u", userId);

                    case "Email":
                        return GetList("SELECT EmailAddress FROM UserEmail WHERE UserID=@u", "@u", userId);

                    case "location":
                        using (var conn = new SqlConnection(connStr))
                        {
                            conn.Open();
                            using var cmd = new SqlCommand(
                                @"SELECT l.LocationDescription
                                  FROM CompUser c
                                  LEFT JOIN Location l ON c.LocationID=l.LocationID
                                  WHERE c.UserID=@u", conn);
                            cmd.Parameters.AddWithValue("@u", userId);
                            return cmd.ExecuteScalar() as string;
                        }

                    default:
                        return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DB error in GetField: {ex.Message} (check field and tables)");
                return null;
            }
        }

        static void Lookup(string login, string field)
        {
            if (debug) Console.WriteLine($"Lookup field '{field}'");
            var value = GetField(login, field);
            if (value == null)
            {
                Console.WriteLine($"User '{login}' not known");
                return;
            }
            Console.WriteLine(value);
        }

        static void Update(string login, string field, string value)
        {
            if (debug) Console.WriteLine($"Update field '{field}' to '{value}'");

            string userId = EnsureUser(login);

            try
            {
                switch (field)
                {
                    case "Surname":
                    case "Title":
                        using (var conn = new SqlConnection(connStr))
                        {
                            conn.Open();
                            using var cmd = new SqlCommand(
                                $"UPDATE CompUser SET {field}=@v WHERE UserID=@u", conn);
                            cmd.Parameters.AddWithValue("@v", (object)value ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@u", userId);
                            cmd.ExecuteNonQuery();
                        }
                        break;

                    case "Fornames":
                        using (var conn = new SqlConnection(connStr))
                        {
                            conn.Open();
                            using (var del = new SqlCommand(
                                "DELETE FROM UserForename WHERE UserID=@u", conn))
                            {
                                del.Parameters.AddWithValue("@u", userId);
                                del.ExecuteNonQuery();
                            }

                            var parts = (value ?? "")
                                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            int ord = 1;
                            foreach (var fn in parts)
                            {
                                using var ins = new SqlCommand(
                                    "INSERT INTO UserForename(UserID,ForenameOrder,Forename) VALUES(@u,@o,@f)", conn);
                                ins.Parameters.AddWithValue("@u", userId);
                                ins.Parameters.AddWithValue("@o", ord++);
                                ins.Parameters.AddWithValue("@f", fn);
                                ins.ExecuteNonQuery();
                            }
                        }
                        break;

                    case "Position":
                        {
                            int posId = GetOrCreateId(
                                "SELECT PositionID FROM Position WHERE PositionName=@n",
                                "INSERT INTO Position(PositionName) VALUES(@n);SELECT SCOPE_IDENTITY();",
                                "@n", value);

                            using var conn = new SqlConnection(connStr);
                            conn.Open();
                            using (var del = new SqlCommand(
                                "DELETE FROM UserPosition WHERE UserID=@u", conn))
                            {
                                del.Parameters.AddWithValue("@u", userId);
                                del.ExecuteNonQuery();
                            }
                            using var ins = new SqlCommand(
                                "INSERT INTO UserPosition(UserID,PositionID) VALUES(@u,@p)", conn);
                            ins.Parameters.AddWithValue("@u", userId);
                            ins.Parameters.AddWithValue("@p", posId);
                            ins.ExecuteNonQuery();
                        }
                        break;

                    case "Phone":
                        using (var conn = new SqlConnection(connStr))
                        {
                            conn.Open();
                            using (var del = new SqlCommand(
                                "DELETE FROM UserPhone WHERE UserID=@u", conn))
                            {
                                del.Parameters.AddWithValue("@u", userId);
                                del.ExecuteNonQuery();
                            }
                            if (!string.IsNullOrEmpty(value))
                            {
                                using (var insPhone = new SqlCommand(
                                    "IF NOT EXISTS(SELECT 1 FROM Phone WHERE PhoneNumber=@p) " +
                                    "INSERT INTO Phone(PhoneNumber) VALUES(@p)", conn))
                                {
                                    insPhone.Parameters.AddWithValue("@p", value);
                                    insPhone.ExecuteNonQuery();
                                }
                                using var ins = new SqlCommand(
                                    "INSERT INTO UserPhone(UserID,PhoneNumber) VALUES(@u,@p)", conn);
                                ins.Parameters.AddWithValue("@u", userId);
                                ins.Parameters.AddWithValue("@p", value);
                                ins.ExecuteNonQuery();
                            }
                        }
                        break;

                    case "Email":
                        using (var conn = new SqlConnection(connStr))
                        {
                            conn.Open();
                            using (var del = new SqlCommand(
                                "DELETE FROM UserEmail WHERE UserID=@u", conn))
                            {
                                del.Parameters.AddWithValue("@u", userId);
                                del.ExecuteNonQuery();
                            }
                            if (!string.IsNullOrEmpty(value))
                            {
                                using (var insEmail = new SqlCommand(
                                    "IF NOT EXISTS(SELECT 1 FROM Email WHERE EmailAddress=@e) " +
                                    "INSERT INTO Email(EmailAddress) VALUES(@e)", conn))
                                {
                                    insEmail.Parameters.AddWithValue("@e", value);
                                    insEmail.ExecuteNonQuery();
                                }
                                using var ins = new SqlCommand(
                                    "INSERT INTO UserEmail(UserID,EmailAddress) VALUES(@u,@e)", conn);
                                ins.Parameters.AddWithValue("@u", userId);
                                ins.Parameters.AddWithValue("@e", value);
                                ins.ExecuteNonQuery();
                            }
                        }
                        break;

                    case "location":
                        {
                            int locId = GetOrCreateId(
                                "SELECT LocationID FROM Location WHERE LocationDescription=@n",
                                "INSERT INTO Location(LocationID,LocationDescription) " +
                                "VALUES((SELECT ISNULL(MAX(LocationID),0)+1 FROM Location),@n); " +
                                "SELECT MAX(LocationID) FROM Location;",
                                "@n", value);

                            using var conn = new SqlConnection(connStr);
                            conn.Open();
                            using var cmd = new SqlCommand(
                                "UPDATE CompUser SET LocationID=@loc WHERE UserID=@u", conn);
                            cmd.Parameters.AddWithValue("@loc", locId);
                            cmd.Parameters.AddWithValue("@u", userId);
                            cmd.ExecuteNonQuery();
                        }
                        break;
                }

                Console.WriteLine("OK");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DB error in Update: {ex.Message} (check field name and related tables)");
            }
        }

        static void DeleteUser(string login)
        {
            if (debug) Console.WriteLine($"Delete '{login}'");
            string? userId = GetUserId(login);

            try
            {
                using var conn = new SqlConnection(connStr);
                conn.Open();

                if (userId != null)
                {
                    void Del(string sql)
                    {
                        using var cmd = new SqlCommand(sql, conn);
                        cmd.Parameters.AddWithValue("@u", userId);
                        cmd.ExecuteNonQuery();
                    }

                    Del("DELETE FROM UserEmail WHERE UserID=@u");
                    Del("DELETE FROM UserPhone WHERE UserID=@u");
                    Del("DELETE FROM UserPosition WHERE UserID=@u");
                    Del("DELETE FROM UserForename WHERE UserID=@u");
                    Del("DELETE FROM UserLogin WHERE UserID=@u");
                    Del("DELETE FROM CompUser WHERE UserID=@u");
                }

                using (var cmd = new SqlCommand(
                    "DELETE FROM LoginAccount WHERE LoginID=@l", conn))
                {
                    cmd.Parameters.AddWithValue("@l", login);
                    cmd.ExecuteNonQuery();
                }

                Console.WriteLine("OK");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DB error in DeleteUser: {ex.Message} (check foreign keys and tables)");
            }
        }

        // whois command parsing

        static void HandleCommand(string cmd)
        {
            if (debug) Console.WriteLine($"\nCommand: {cmd}");
            try
            {
                string[] parts = cmd.Split(new char[] { '?' }, 2);
                string login = parts[0];
                string? op = null;
                string? field = null;
                string? value = null;

                if (parts.Length == 2)
                {
                    op = parts[1];
                    if (op == "")
                    {
                        DeleteUser(login);
                        return;
                    }

                    string[] p2 = op.Split(new char[] { '=' }, 2);
                    field = p2[0];
                    if (p2.Length == 2) value = p2[1];
                }

                if (op == null && value == null)
                {
                    Dump(login);
                }
                else if (op != null && value == null)
                {
                    Lookup(login, field!);
                }
                else if (op != null && value != null)
                {
                    Update(login, field!, value);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HandleCommand: {ex.Message} (check command format like cssbct or cssbct?field=value)");
            }
        }

        // HTTP wrappers for location

        static string? GetLocation(string login) => GetField(login, "location");

        static void SetLocation(string login, string loc) => Update(login, "location", loc);

        // HTTP request handling

        static void HandleHttp(NetworkStream stream)
        {
            using var sw = new StreamWriter(stream) { AutoFlush = true };
            using var sr = new StreamReader(stream);

            try
            {
                string? line = sr.ReadLine();
                if (line == null)
                {
                    if (debug) Console.WriteLine("Empty HTTP request");
                    return;
                }

                if (debug) Console.WriteLine($"HTTP: {line}");

                // POST / HTTP/1.1 update
                if (line == "POST / HTTP/1.1")
                {
                    int len = 0;

                    while (!string.IsNullOrEmpty(line = sr.ReadLine()))
                    {
                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        {
                            string s = line.Substring("Content-Length:".Length).Trim();
                            int.TryParse(s, out len);
                        }
                    }

                    char[] buf = new char[len];
                    int read = 0;
                    while (read < len)
                    {
                        int r = sr.Read(buf, read, len - read);
                        if (r <= 0) break;
                        read += r;
                    }

                    string body = new string(buf, 0, read);
                    if (debug) Console.WriteLine($"Body: {body}");

                    string? name = null;
                    string? loc = null;
                    foreach (var part in body.Split('&'))
                    {
                        if (part.StartsWith("name=")) name = part.Substring(5);
                        else if (part.StartsWith("location=")) loc = part.Substring(9);
                    }

                    if (string.IsNullOrEmpty(name) || loc == null)
                    {
                        sw.WriteLine("HTTP/1.1 400 Bad Request");
                        sw.WriteLine("Content-Type: text/plain");
                        sw.WriteLine();
                        if (debug) Console.WriteLine("POST body incorrect, expected name and location");
                        return;
                    }

                    SetLocation(name, loc);

                    sw.WriteLine("HTTP/1.1 200 OK");
                    sw.WriteLine("Content-Type: text/plain");
                    sw.WriteLine();
                    if (debug) Console.WriteLine($"Location updated: {name} -> {loc}");
                }
                // GET /?name=... HTTP/1.1 lookup
                else if (line.StartsWith("GET /?name=") && line.EndsWith(" HTTP/1.1"))
                {
                    string[] first = line.Split(' ');
                    if (first.Length < 2)
                    {
                        sw.WriteLine("HTTP/1.1 400 Bad Request");
                        sw.WriteLine("Content-Type: text/plain");
                        sw.WriteLine();
                        return;
                    }

                    string path = first[1];
                    string name = path.Substring("/?name=".Length);

                    while (!string.IsNullOrEmpty(line = sr.ReadLine())) { }

                    string? loc = GetLocation(name);
                    if (loc != null)
                    {
                        sw.WriteLine("HTTP/1.1 200 OK");
                        sw.WriteLine("Content-Type: text/plain");
                        sw.WriteLine();
                        sw.WriteLine(loc);
                        if (debug) Console.WriteLine($"Lookup {name} -> {loc}");
                    }
                    else
                    {
                        sw.WriteLine("HTTP/1.1 404 Not Found");
                        sw.WriteLine("Content-Type: text/plain");
                        sw.WriteLine();
                        if (debug) Console.WriteLine($"Lookup {name} not found");
                    }
                }
                else
                {
                    sw.WriteLine("HTTP/1.1 400 Bad Request");
                    sw.WriteLine("Content-Type: text/plain");
                    sw.WriteLine();
                    if (debug) Console.WriteLine("Unknown HTTP request line");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HandleHttp: {ex.Message} (check HTTP format or timeout)");
            }
        }

        // TCP server

        static void RunServer()
        {
            try
            {
                var listener = new TcpListener(IPAddress.Any, 443);
                listener.Start();
                if (debug) Console.WriteLine("Listening on port 443");

                while (true)
                {
                    var socket = listener.AcceptSocket();
                    if (debug) Console.WriteLine("Connected");

                    Task.Run(() =>
                    {
                        using (socket)
                        using (var stream = new NetworkStream(socket))
                        {
                            socket.ReceiveTimeout = 1000;
                            socket.SendTimeout = 1000;
                            HandleHttp(stream);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Server error in RunServer: {ex.Message} (port 443 errors)");
            }
        }
    }
}
