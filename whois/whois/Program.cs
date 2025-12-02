using System;
using System.IO;
using System.Net.Sockets;
using System.Data.SqlClient;

bool debug = true;

string connectionString =
    "Server=localhost;Database=AssignmentDatabase;Trusted_Connection=True;";



string? GetUserIdForLogin(string loginId)
{
    using var conn = new SqlConnection(connectionString);
    conn.Open();
    using var cmd = new SqlCommand(
        "SELECT TOP 1 UserID FROM UserLogin WHERE LoginID=@login", conn);
    cmd.Parameters.AddWithValue("@login", loginId);
    var obj = cmd.ExecuteScalar();
    return obj as string;
}

string GetOrCreateUserIdForLogin(string loginId)
{
    // Try existing first
    var existing = GetUserIdForLogin(loginId);
    if (!string.IsNullOrEmpty(existing)) return existing;

    using var conn = new SqlConnection(connectionString);
    conn.Open();

    // Ensure LoginAccount exists
    using (var cmdCheck = new SqlCommand(
        "SELECT COUNT(*) FROM LoginAccount WHERE LoginID=@login", conn))
    {
        cmdCheck.Parameters.AddWithValue("@login", loginId);
        var count = (int)cmdCheck.ExecuteScalar();
        if (count == 0)
        {
            using var cmdIns = new SqlCommand(
                "INSERT INTO LoginAccount(LoginID) VALUES(@login)", conn);
            cmdIns.Parameters.AddWithValue("@login", loginId);
            cmdIns.ExecuteNonQuery();
        }
    }

    // Create a new user
    string userId = Guid.NewGuid().ToString();

    using (var cmdUser = new SqlCommand(
        "INSERT INTO CompUser(UserID,Surname,Title,LocationID) VALUES(@uid,NULL,NULL,NULL)", conn))
    {
        cmdUser.Parameters.AddWithValue("@uid", userId);
        cmdUser.ExecuteNonQuery();
    }

    // Link login → user
    using (var cmdLink = new SqlCommand(
        "INSERT INTO UserLogin(UserID,LoginID) VALUES(@uid,@login)", conn))
    {
        cmdLink.Parameters.AddWithValue("@uid", userId);
        cmdLink.Parameters.AddWithValue("@login", loginId);
        cmdLink.ExecuteNonQuery();
    }

    return userId;
}

string GetForenamesFromDb(string userId)
{
    using var conn = new SqlConnection(connectionString);
    conn.Open();
    using var cmd = new SqlCommand(
        "SELECT Forename FROM UserForename WHERE UserID=@uid ORDER BY ForenameOrder", conn);
    cmd.Parameters.AddWithValue("@uid", userId);
    using var r = cmd.ExecuteReader();
    var parts = new System.Collections.Generic.List<string>();
    while (r.Read())
        parts.Add(r["Forename"] as string ?? "");
    return string.Join(" ", parts);
}

string GetPositionsFromDb(string userId)
{
    using var conn = new SqlConnection(connectionString);
    conn.Open();
    using var cmd = new SqlCommand(
        @"SELECT p.PositionName
          FROM UserPosition up
          JOIN Position p ON up.PositionID=p.PositionID
          WHERE up.UserID=@uid", conn);
    cmd.Parameters.AddWithValue("@uid", userId);
    using var r = cmd.ExecuteReader();
    var parts = new System.Collections.Generic.List<string>();
    while (r.Read())
        parts.Add(r["PositionName"] as string ?? "");
    return string.Join(", ", parts);
}

string GetListFromDb(string sql, string paramName, string paramVal)
{
    using var conn = new SqlConnection(connectionString);
    conn.Open();
    using var cmd = new SqlCommand(sql, conn);
    cmd.Parameters.AddWithValue(paramName, paramVal);
    using var r = cmd.ExecuteReader();
    var parts = new System.Collections.Generic.List<string>();
    while (r.Read())
        parts.Add(r[0] as string ?? "");
    return string.Join(", ", parts);
}

string? ScalarString(string sql, string paramName, string paramVal)
{
    using var conn = new SqlConnection(connectionString);
    conn.Open();
    using var cmd = new SqlCommand(sql, conn);
    cmd.Parameters.AddWithValue(paramName, paramVal);
    var obj = cmd.ExecuteScalar();
    return obj as string;
}

int GetOrCreateId(string selectSql, string insertSql,
                  string paramName, string paramVal)
{
    using var conn = new SqlConnection(connectionString);
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

// ====== Core operations: Dump, Lookup, Update, Delete ======

void Dump(string ID)
{
    if (debug) Console.WriteLine(" output all fields");
    string? userId = GetUserIdForLogin(ID);
    if (userId == null)
    {
        Console.WriteLine("No such user");
        return;
    }

    string? surname = null;
    string? title = null;
    string? location = null;

    using (var conn = new SqlConnection(connectionString))
    {
        conn.Open();
        using var cmd = new SqlCommand(
            @"SELECT c.Surname,c.Title,l.LocationDescription
              FROM CompUser c
              LEFT JOIN Location l ON c.LocationID=l.LocationID
              WHERE c.UserID=@uid", conn);
        cmd.Parameters.AddWithValue("@uid", userId);
        using var r = cmd.ExecuteReader();
        if (r.Read())
        {
            surname = r["Surname"] as string;
            title = r["Title"] as string;
            location = r["LocationDescription"] as string;
        }
    }

    var forenames = GetForenamesFromDb(userId);
    var positions = GetPositionsFromDb(userId);
    var phones = GetListFromDb(
        "SELECT PhoneNumber FROM UserPhone WHERE UserID=@uid", "@uid", userId);
    var emails = GetListFromDb(
        "SELECT EmailAddress FROM UserEmail WHERE UserID=@uid", "@uid", userId);

    Console.WriteLine($"UserID={userId}");
    Console.WriteLine($"Surname={surname ?? ""}");
    Console.WriteLine($"Fornames={forenames}");
    Console.WriteLine($"Title={title ?? ""}");
    Console.WriteLine($"Position={positions}");
    Console.WriteLine($"Phone={phones}");
    Console.WriteLine($"Email={emails}");
    Console.WriteLine($"location={location ?? ""}");
}

string? GetField(string ID, string field)
{
    string? userId = GetUserIdForLogin(ID);
    if (userId == null) return null;

    switch (field)
    {
        case "UserID":
            return userId;
        case "Surname":
            return ScalarString("SELECT Surname FROM CompUser WHERE UserID=@uid", "@uid", userId);
        case "Fornames":
            return GetForenamesFromDb(userId);
        case "Title":
            return ScalarString("SELECT Title FROM CompUser WHERE UserID=@uid", "@uid", userId);
        case "Position":
            return GetPositionsFromDb(userId);
        case "Phone":
            return GetListFromDb("SELECT PhoneNumber FROM UserPhone WHERE UserID=@uid", "@uid", userId);
        case "Email":
            return GetListFromDb("SELECT EmailAddress FROM UserEmail WHERE UserID=@uid", "@uid", userId);
        case "location":
            return ScalarString(
                @"SELECT l.LocationDescription
                  FROM CompUser c LEFT JOIN Location l ON c.LocationID=l.LocationID
                  WHERE c.UserID=@uid", "@uid", userId);
        default:
            return null;
    }
}

void Lookup(string ID, string field)
{
    if (debug) Console.WriteLine($" lookup field '{field}'");
    var result = GetField(ID, field);
    if (result == null)
    {
        Console.WriteLine("No such user");
        return;
    }
    Console.WriteLine(result);
}

void Update(string ID, string field, string update)
{
    if (debug) Console.WriteLine($" update field '{field}' to '{update}'");

    string userId = GetOrCreateUserIdForLogin(ID);

    switch (field)
    {
        case "Surname":
        case "Title":
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using var cmd = new SqlCommand(
                    $"UPDATE CompUser SET {field}=@v WHERE UserID=@uid", conn);
                cmd.Parameters.AddWithValue("@v", (object)update ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@uid", userId);
                cmd.ExecuteNonQuery();
            }
            break;

        case "Fornames":
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var del = new SqlCommand(
                    "DELETE FROM UserForename WHERE UserID=@uid", conn))
                {
                    del.Parameters.AddWithValue("@uid", userId);
                    del.ExecuteNonQuery();
                }

                var parts = (update ?? "")
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                int order = 1;
                foreach (var fn in parts)
                {
                    using var ins = new SqlCommand(
                        "INSERT INTO UserForename(UserID,ForenameOrder,Forename) VALUES(@uid,@ord,@fn)", conn);
                    ins.Parameters.AddWithValue("@uid", userId);
                    ins.Parameters.AddWithValue("@ord", order++);
                    ins.Parameters.AddWithValue("@fn", fn);
                    ins.ExecuteNonQuery();
                }
            }
            break;

        case "Position":
            {
                int posId = GetOrCreateId(
                    "SELECT PositionID FROM Position WHERE PositionName=@name",
                    "INSERT INTO Position(PositionName) VALUES(@name);SELECT SCOPE_IDENTITY();",
                    "@name", update);

                using var conn = new SqlConnection(connectionString);
                conn.Open();
                using (var del = new SqlCommand(
                    "DELETE FROM UserPosition WHERE UserID=@uid", conn))
                {
                    del.Parameters.AddWithValue("@uid", userId);
                    del.ExecuteNonQuery();
                }
                using var ins = new SqlCommand(
                    "INSERT INTO UserPosition(UserID,PositionID) VALUES(@uid,@pid)", conn);
                ins.Parameters.AddWithValue("@uid", userId);
                ins.Parameters.AddWithValue("@pid", posId);
                ins.ExecuteNonQuery();
            }
            break;

        case "Phone":
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var del = new SqlCommand(
                    "DELETE FROM UserPhone WHERE UserID=@uid", conn))
                {
                    del.Parameters.AddWithValue("@uid", userId);
                    del.ExecuteNonQuery();
                }
                if (!string.IsNullOrEmpty(update))
                {
                    using (var insPhone = new SqlCommand(
                        "IF NOT EXISTS(SELECT 1 FROM Phone WHERE PhoneNumber=@p) " +
                        "INSERT INTO Phone(PhoneNumber) VALUES(@p)", conn))
                    {
                        insPhone.Parameters.AddWithValue("@p", update);
                        insPhone.ExecuteNonQuery();
                    }
                    using var ins = new SqlCommand(
                        "INSERT INTO UserPhone(UserID,PhoneNumber) VALUES(@uid,@p)", conn);
                    ins.Parameters.AddWithValue("@uid", userId);
                    ins.Parameters.AddWithValue("@p", update);
                    ins.ExecuteNonQuery();
                }
            }
            break;

        case "Email":
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var del = new SqlCommand(
                    "DELETE FROM UserEmail WHERE UserID=@uid", conn))
                {
                    del.Parameters.AddWithValue("@uid", userId);
                    del.ExecuteNonQuery();
                }
                if (!string.IsNullOrEmpty(update))
                {
                    using (var insEmail = new SqlCommand(
                        "IF NOT EXISTS(SELECT 1 FROM Email WHERE EmailAddress=@e) " +
                        "INSERT INTO Email(EmailAddress) VALUES(@e)", conn))
                    {
                        insEmail.Parameters.AddWithValue("@e", update);
                        insEmail.ExecuteNonQuery();
                    }
                    using var ins = new SqlCommand(
                        "INSERT INTO UserEmail(UserID,EmailAddress) VALUES(@uid,@e)", conn);
                    ins.Parameters.AddWithValue("@uid", userId);
                    ins.Parameters.AddWithValue("@e", update);
                    ins.ExecuteNonQuery();
                }
            }
            break;

        case "location":
            {
                int locId = GetOrCreateId(
                    "SELECT LocationID FROM Location WHERE LocationDescription=@name",
                    "INSERT INTO Location(LocationID,LocationDescription) " +
                    "VALUES((SELECT ISNULL(MAX(LocationID),0)+1 FROM Location),@name); " +
                    "SELECT MAX(LocationID) FROM Location;",
                    "@name", update);

                using var conn = new SqlConnection(connectionString);
                conn.Open();
                using var cmd = new SqlCommand(
                    "UPDATE CompUser SET LocationID=@loc WHERE UserID=@uid", conn);
                cmd.Parameters.AddWithValue("@loc", locId);
                cmd.Parameters.AddWithValue("@uid", userId);
                cmd.ExecuteNonQuery();
            }
            break;
    }

    Console.WriteLine("OK");
}

void Delete(string ID)
{
    if (debug) Console.WriteLine($"Delete record '{ID}'");
    string? userId = GetUserIdForLogin(ID);
    using var conn = new SqlConnection(connectionString);
    conn.Open();

    if (userId != null)
    {
        void Del(string sql, string paramName, string val)
        {
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue(paramName, val);
            cmd.ExecuteNonQuery();
        }

        Del("DELETE FROM UserEmail WHERE UserID=@u", "@u", userId);
        Del("DELETE FROM UserPhone WHERE UserID=@u", "@u", userId);
        Del("DELETE FROM UserPosition WHERE UserID=@u", "@u", userId);
        Del("DELETE FROM UserForename WHERE UserID=@u", "@u", userId);
        Del("DELETE FROM UserLogin WHERE UserID=@u", "@u", userId);
        Del("DELETE FROM CompUser WHERE UserID=@u", "@u", userId);
    }

    using (var cmd = new SqlCommand(
        "DELETE FROM LoginAccount WHERE LoginID=@login", conn))
    {
        cmd.Parameters.AddWithValue("@login", ID);
        cmd.ExecuteNonQuery();
    }

    Console.WriteLine("OK");
}

// ====== Command processing ======

void ProcessCommand(string command)
{
    if (debug) Console.WriteLine($"\nCommand: {command}");
    try
    {
        string[] slice = command.Split(new char[] { '?' }, 2);
        string ID = slice[0];
        string? operation = null;
        string? update = null;
        string? field = null;

        if (slice.Length == 2)
        {
            operation = slice[1];
            if (operation == "")
            {
                // delete
                Delete(ID);
                return;
            }
            string[] pieces = operation.Split(new char[] { '=' }, 2);
            field = pieces[0];
            if (pieces.Length == 2) update = pieces[1];
        }

        if (debug) Console.Write($"Operation on ID '{ID}'");

        if (operation == null && update == null)
        {
            // dump
            Dump(ID);
        }
        else if (operation != null && update == null)
        {
            // lookup
            Lookup(ID, field!);
        }
        else if (operation != null && update != null)
        {
            // update (create if needed)
            Update(ID, field!, update);
        }
    }
    catch (Exception e)
    {
        Console.WriteLine($"Fault in Command Processing: {e}");
    }
}

// ====== HTTP server parts (using DB for location) ======

string? GetLocationForLogin(string id) => GetField(id, "location");

void SetLocationForLogin(string id, string value) => Update(id, "location", value);

void doRequest(NetworkStream socketStream)
{
    var sw = new StreamWriter(socketStream);
    var sr = new StreamReader(socketStream);

    if (debug) Console.WriteLine("Waiting for input from client...");
    try
    {
        string? line = sr.ReadLine();
        Console.WriteLine($"Received Network Command: '{line}'");
        if (line == null)
        {
            if (debug) Console.WriteLine("Ignoring null command");
            return;
        }

        if (line == "POST / HTTP/1.1")
        {
            if (debug) Console.WriteLine("Received an update request");
            int content_length = 0;
            while (line != "")
            {
                if (line.StartsWith("Content-Length: "))
                {
                    content_length = Int32.Parse(line.Substring(16));
                }
                line = sr.ReadLine();
                if (debug) Console.WriteLine($"Skipped Header Line: '{line}'");
            }

            try
            {
                string newline = "";
                for (int i = 0; i < content_length; i++)
                {
                    char c = (char)sr.Read();
                    newline += c;
                }
                string[] slices = newline.Split(new char[] { '&' }, 2);
                if (slices.Length < 2 || !slices[0].StartsWith("name=") || !slices[1].StartsWith("location="))
                {
                    sw.WriteLine("HTTP/1.1 400 Bad Request");
                    sw.WriteLine("Content-Type: text/plain");
                    sw.WriteLine();
                    sw.Flush();
                    Console.WriteLine($"Unrecognised command body: '{newline}'");
                    return;
                }
                string ID = slices[0].Substring(5);
                string value = slices[1].Substring(9);
                if (debug) Console.WriteLine($"Received an update request for '{ID}' to '{value}'");
                SetLocationForLogin(ID, value);

                sw.WriteLine("HTTP/1.1 200 OK");
                sw.WriteLine("Content-Type: text/plain");
                sw.WriteLine();
                sw.WriteLine("OK");
                sw.Flush();
            }
            catch (Exception e) { Console.WriteLine(e.ToString()); }
        }
        else if (line.StartsWith("GET /?name=") && line.EndsWith(" HTTP/1.1"))
        {
            string[] slices = line.Split(" ");
            string ID = slices[1].Substring(7);
            if (debug) Console.WriteLine("Received a lookup request");

            string? result = GetLocationForLogin(ID);
            if (result != null)
            {
                sw.WriteLine("HTTP/1.1 200 OK");
                sw.WriteLine("Content-Type: text/plain");
                sw.WriteLine();
                sw.WriteLine(result);
                sw.Flush();
                Console.WriteLine($"Performed Lookup on '{ID}' returning '{result}'");
            }
            else
            {
                sw.WriteLine("HTTP/1.1 404 Not Found");
                sw.WriteLine("Content-Type: text/plain");
                sw.WriteLine();
                sw.Flush();
                Console.WriteLine($"Performed Lookup on '{ID}' returning '404 Not Found'");
            }
        }
        else
        {
            Console.WriteLine("Unrecognised command");
            sw.WriteLine("HTTP/1.1 400 Bad Request");
            sw.WriteLine("Content-Type: text/plain");
            sw.WriteLine();
            sw.Flush();
        }
    }
    catch (Exception e)
    {
        Console.WriteLine($"Fault in Network Command Processing: {e}");
    }
    finally
    {
        sw.Close();
        sr.Close();
    }
}

void RunServer()
{
    TcpListener listener;
    Socket connection;
    NetworkStream socketStream;
    try
    {
        listener = new TcpListener(443);
        listener.Start();
        while (true)
        {
            if (debug) Console.WriteLine("Server Waiting connection...");
            connection = listener.AcceptSocket();
            socketStream = new NetworkStream(connection);
            doRequest(socketStream);
            socketStream.Close();
            connection.Close();
        }
    }
    catch (Exception e)
    {
        Console.WriteLine(e.ToString());
    }
    if (debug) Console.WriteLine("Terminating Server");
}

// ====== Entry point behavior ======

for (int i = 0; i < args.Length; i++)
{
    if (debug) Console.WriteLine(args[i]);
}

if (args.Length == 0)
{
    Console.WriteLine("Starting Server");
    RunServer();
}
else
{
    for (int i = 0; i < args.Length; i++)
    {
        ProcessCommand(args[i]);
    }
}