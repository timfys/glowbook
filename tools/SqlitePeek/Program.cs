using Microsoft.Data.Sqlite;
var path = @"C:\Users\timofey\RiderProjects\glowbook_git\src\GlowBook.Web\Data\glowbook.db";
using var c = new SqliteConnection($"Data Source={path}");
c.Open();
using (var cmd = c.CreateCommand()) {
  cmd.CommandText = "PRAGMA wal_checkpoint(FULL);";
  Console.WriteLine("checkpoint=" + cmd.ExecuteScalar());
}
using (var cmd = c.CreateCommand()) {
  cmd.CommandText = "SELECT Id, Email, UserName, DisplayName FROM AspNetUsers";
  using var r = cmd.ExecuteReader();
  while (r.Read()) Console.WriteLine($"user {r[0]} | {r[1]} | {r[2]} | {r[3]}");
}
using (var cmd = c.CreateCommand()) {
  cmd.CommandText = "SELECT Id, BusinessName, BookingSlug, UserId FROM MasterProfiles";
  using var r = cmd.ExecuteReader();
  while (r.Read()) Console.WriteLine($"profile {r[0]} | {r[1]} | {r[2]} | {r[3]}");
}
Console.WriteLine("db size after=" + new FileInfo(path).Length);
