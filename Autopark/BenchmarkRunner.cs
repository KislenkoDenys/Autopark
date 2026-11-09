using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.Data.SqlClient;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AutoPark
{
    public class BenchmarkRunner
    {
        private readonly string _sqlConnString;
        private readonly IMongoCollection<BsonDocument> _mongoCollection;

        public BenchmarkRunner(string sqlConnString, string mongoConnString = "mongodb://localhost:27017")
        {
            _sqlConnString = sqlConnString;
            var mongoClient = new MongoClient(mongoConnString);
            var mongoDb = mongoClient.GetDatabase("FleetManagement");
            _mongoCollection = mongoDb.GetCollection<BsonDocument>("trips_benchmark");
        }

        public void RunAll(int recordCount = 5000)
        {
            CleanDatabases();

            long sqlInsertTime = BenchmarkSqlInsert(recordCount);
            long mongoInsertTime = BenchmarkMongoInsert(recordCount);

            long sqlReadTime = BenchmarkSqlRead();
            long mongoReadTime = BenchmarkMongoRead();

            long sqlAggTime = BenchmarkSqlAggregation();
            long mongoAggTime = BenchmarkMongoAggregation();

            Console.WriteLine($"Insert SQL: {sqlInsertTime} ms");
            Console.WriteLine($"Insert MongoDB: {mongoInsertTime} ms");
            Console.WriteLine($"Read SQL: {sqlReadTime} ms");
            Console.WriteLine($"Read MongoDB: {mongoReadTime} ms");
            Console.WriteLine($"Aggregation SQL: {sqlAggTime} ms");
            Console.WriteLine($"Aggregation MongoDB: {mongoAggTime} ms");
        }

        private void CleanDatabases()
        {
            using (var conn = new SqlConnection(_sqlConnString))
            {
                conn.Open();
                using var cmd = new SqlCommand("DELETE FROM Trips WHERE start_date >= '2026-01-01'", conn);
                cmd.ExecuteNonQuery();
            }
            _mongoCollection.Database.DropCollection("trips_benchmark");
        }

        private long BenchmarkSqlInsert(int count)
        {
            var sw = Stopwatch.StartNew();
            using (var conn = new SqlConnection(_sqlConnString))
            {
                conn.Open();
                int routeId, vehicleId, driverId, clientId, statusId;
                using (var cmdIds = conn.CreateCommand())
                {
                    cmdIds.CommandText = @"
                SELECT 
                    (SELECT TOP 1 id FROM Routes) AS route_id,
                    (SELECT TOP 1 id FROM Vehicles) AS vehicle_id,
                    (SELECT TOP 1 id FROM Drivers) AS driver_id,
                    (SELECT TOP 1 id FROM Clients) AS client_id,
                    (SELECT TOP 1 id FROM Trip_Statuses) AS status_id;";

                    using var reader = cmdIds.ExecuteReader();
                    if (!reader.Read()) throw new Exception("Довідники порожні!");
                    routeId = reader.GetInt32(0);
                    vehicleId = reader.GetInt32(1);
                    driverId = reader.GetInt32(2);
                    clientId = reader.GetInt32(3);
                    statusId = reader.GetInt32(4);
                }

                using (var trans = conn.BeginTransaction())
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = trans;
                        cmd.CommandText = $@"
                    INSERT INTO Trips (route_id, vehicle_id, driver_id, client_id, status_id, start_date, end_date) 
                    VALUES ({routeId}, {vehicleId}, {driverId}, {clientId}, {statusId}, @start, @end)";

                        var pStart = cmd.Parameters.Add("@start", System.Data.SqlDbType.DateTime);
                        var pEnd = cmd.Parameters.Add("@end", System.Data.SqlDbType.DateTime);

                        for (int i = 0; i < count; i++)
                        {
                            pStart.Value = DateTime.Now;
                            pEnd.Value = DateTime.Now.AddHours(5);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    trans.Commit();
                }
            }
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }

        private long BenchmarkMongoInsert(int count)
        {
            var documents = new List<BsonDocument>(count);
            for (int i = 0; i < count; i++)
            {
                documents.Add(new BsonDocument
                {
                    { "trip_id", i + 1 },
                    { "status", "Completed" },
                    { "start_date", DateTime.UtcNow },
                    { "end_date", DateTime.UtcNow.AddHours(5) },
                    { "client", new BsonDocument { { "client_id", 1 }, { "company_name", "ТОВ «Логістик Транс»" } } },
                    { "driver", new BsonDocument { { "driver_id", 1 }, { "full_name", "Іван Коваль" } } },
                    { "vehicle", new BsonDocument { { "vehicle_id", 1 }, { "license_plate", "AA1234BB" } } },
                    { "route", new BsonDocument {
                        { "start_city", "Київ" },
                        { "end_city", "Львів" },
                        { "distance_km", 540.5 }
                    }},
                    { "fuel_logs", new BsonArray {
                        new BsonDocument { { "liters", 100 }, { "cost", 5500 } }
                    }}
                });
            }

            var sw = Stopwatch.StartNew();
            _mongoCollection.InsertMany(documents);
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }

        private long BenchmarkSqlRead()
        {
            var sw = Stopwatch.StartNew();
            using (var conn = new SqlConnection(_sqlConnString))
            {
                conn.Open();
                string query = @"
                    SELECT t.id, c.company_name, d.first_name, d.last_name, v.license_plate, 
                           r.distance_km, l1.city AS StartCity, l2.city AS EndCity
                    FROM Trips t
                    JOIN Clients c ON t.client_id = c.id
                    JOIN Drivers d ON t.driver_id = d.id
                    JOIN Vehicles v ON t.vehicle_id = v.id
                    JOIN Routes r ON t.route_id = r.id
                    JOIN Locations l1 ON r.start_location_id = l1.id
                    JOIN Locations l2 ON r.end_location_id = l2.id";

                using (var cmd = new SqlCommand(query, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read()) { }
                }
            }
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }

        private long BenchmarkMongoRead()
        {
            var sw = Stopwatch.StartNew();
            var results = _mongoCollection.Find(new BsonDocument()).ToList();
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }

        private long BenchmarkSqlAggregation()
        {
            var sw = Stopwatch.StartNew();
            using (var conn = new SqlConnection(_sqlConnString))
            {
                conn.Open();
                string query = @"
                    SELECT SUM(r.distance_km) 
                    FROM Trips t
                    JOIN Routes r ON t.route_id = r.id";

                using (var cmd = new SqlCommand(query, conn))
                {
                    var res = cmd.ExecuteScalar();
                }
            }
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }

        private long BenchmarkMongoAggregation()
        {
            var sw = Stopwatch.StartNew();
            var pipeline = new[]
            {
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", BsonNull.Value },
                    { "total_distance", new BsonDocument("$sum", "$route.distance_km") }
                })
            };

            var result = _mongoCollection.Aggregate<BsonDocument>(pipeline).FirstOrDefault();
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }
    }
}