using Microsoft.Data.Sqlite;
foreach (var path in new[] {
  @"C:\Users\timofey\RiderProjects\glowbook_git\src\GlowBook.Web\Data\glowbook.db",
  @"C:\Users\timofey\RiderProjects\glowbook_git\src\GlowBook.Web\app.db"
}) {
  Console.WriteLine("=== " + path + " size=" + new FileInfo(path).Length);
  try {
    using var c = new SqliteConnection($"Data Source={path}");
    c.Open();
    using var t = c.CreateCommand();
    t.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY 1";
    using var r = t.ExecuteReader();
    while (r.Read()) Console.WriteLine(" table: " + r.GetString(0));
    foreach (var q in new[]{"AspNetUsers","Clients","Appointments","MasterProfiles"}) {
      try {
        using var c2 = c.CreateCommand();
        c2.CommandText = $"SELECT COUNT(*) FROM [{q}]";
        Console.WriteLine($" count {q}=" + c2.ExecuteScalar());
      } catch (Exception ex) { Console.WriteLine($" count {q}=ERR {ex.Message}"); }
    }
  } catch (Exception ex) { Console.WriteLine("ERR " + ex.Message); }
}
