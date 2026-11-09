using System;
using System.Threading;
using StackExchange.Redis;

namespace AutoPark
{
    public class RedisService
    {
        private readonly ConnectionMultiplexer _redis;
        private readonly IDatabase _db;

        public RedisService(string connectionString = "localhost:6379")
        {
            _redis = ConnectionMultiplexer.Connect(connectionString);
            _db = _redis.GetDatabase();
        }

        public void RunDemonstration()
        {
            string vehicleKey = "vehicle:1:telemetry";
            _db.KeyDelete(vehicleKey);
            _db.HashSet(vehicleKey, new HashEntry[]
            {
        new HashEntry("status", "On_Trip"),
        new HashEntry("speed", "84"),
        new HashEntry("driver_id", "1"),
        new HashEntry("location", "Kyiv, Polova st.")
            });

            var hashEntries = _db.HashGetAll(vehicleKey);
            foreach (var entry in hashEntries)
            {
                Console.WriteLine($"Hash {entry.Name}: {entry.Value}");
            }

            string sensorKey = "sensor:active:vehicle:1";
            int ttlSeconds = 4;
            _db.StringSet(sensorKey, "online", TimeSpan.FromSeconds(ttlSeconds));
            Console.WriteLine($"TTL Immediate GET: {_db.StringGet(sensorKey)}");

            Thread.Sleep((ttlSeconds + 1) * 1000);
            string expiredVal = _db.StringGet(sensorKey);
            Console.WriteLine($"TTL Expired GET: {(string.IsNullOrEmpty(expiredVal) ? "nil" : expiredVal)}");

            string counterKey = "stats:trips:today";
            _db.KeyDelete(counterKey);
            _db.StringIncrement(counterKey);
            _db.StringIncrement(counterKey);
            _db.StringIncrement(counterKey);
            Console.WriteLine($"Counter INCR value: {_db.StringGet(counterKey)}");
        }
    }
}