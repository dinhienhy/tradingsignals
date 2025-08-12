using System;

namespace TradingSignalsApi
{
    public static class DatabaseUtils
    {
        public static string GetHerokuConnectionString()
        {
            // Get the connection string from the DATABASE_URL environment variable
            string connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (string.IsNullOrEmpty(connectionString))
            {
                return null;
            }

            // Format: postgres://{user}:{password}@{hostname}:{port}/{database-name}
            Uri uri = new Uri(connectionString);
            
            string host = uri.Host;
            int port = uri.Port;
            string userInfo = uri.UserInfo.Split(':')[0];
            string password = uri.UserInfo.Split(':')[1];
            string database = uri.AbsolutePath.TrimStart('/');

            return $"Host={host};Port={port};Username={userInfo};Password={password};Database={database};SSL Mode=Require;Trust Server Certificate=True;";
        }
    }
}
