using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace AutoPark
{
    public class Client
    {
        public string CompanyName { get; set; }
        public string ContactPhone { get; set; }
    }

    public class Vehicle
    {
        public int Id { get; set; }
        public string LicensePlate { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }
    }

    public class ClientRepository
    {
        private readonly SqlConnection _connection;
        private readonly SqlTransaction _transaction;

        public ClientRepository(SqlConnection connection, SqlTransaction transaction)
        {
            _connection = connection;
            _transaction = transaction;
        }

        public void Add(Client client, int? createdByUserId)
        {
            using (SqlCommand cmd = new SqlCommand("sp_AddClient", _connection, _transaction))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@CompanyName", client.CompanyName);
                cmd.Parameters.AddWithValue("@ContactPhone", client.ContactPhone);
                cmd.Parameters.AddWithValue("@CreatedBy", createdByUserId ?? (object)DBNull.Value);

                cmd.ExecuteNonQuery();
            }
        }
    }

    public class VehicleRepository
    {
        private readonly SqlConnection _connection;
        private readonly SqlTransaction _transaction;

        public VehicleRepository(SqlConnection connection, SqlTransaction transaction)
        {
            _connection = connection;
            _transaction = transaction;
        }

        public List<Vehicle> GetActiveVehicles()
        {
            var vehicles = new List<Vehicle>();
            using (SqlCommand cmd = new SqlCommand("SELECT id, license_plate, make, model FROM vw_ActiveVehicles", _connection, _transaction))
            {
                cmd.CommandType = CommandType.Text;
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        vehicles.Add(new Vehicle
                        {
                            Id = reader.GetInt32(0),
                            LicensePlate = reader.GetString(1),
                            Make = reader.IsDBNull(2) ? "Unknown" : reader.GetString(2),
                            Model = reader.IsDBNull(3) ? "Unknown" : reader.GetString(3)
                        });
                    }
                }
            }
            return vehicles;
        }
    }

    public class UnitOfWork : IDisposable
    {
        private readonly SqlConnection _connection;
        private SqlTransaction _transaction;

        public ClientRepository Clients { get; private set; }
        public VehicleRepository Vehicles { get; private set; }

        public UnitOfWork(string connectionString)
        {
            _connection = new SqlConnection(connectionString);
            _connection.Open();
            _transaction = _connection.BeginTransaction();

            Clients = new ClientRepository(_connection, _transaction);
            Vehicles = new VehicleRepository(_connection, _transaction);
        }

        public void Commit()
        {
            try
            {
                _transaction.Commit();
            }
            catch
            {
                _transaction.Rollback();
                throw;
            }
            finally
            {
                _transaction.Dispose();
                _transaction = _connection.BeginTransaction();
            }
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            _connection?.Close();
            _connection?.Dispose();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            string connectionString = @"Server=(localdb)\MSSQLLocalDB;Database=AutoParkDB;Trusted_Connection=True;Encrypt=False;";

            /*var benchmark = new BenchmarkRunner(connectionString);
            benchmark.RunAll(5000);*/

            try
            {
                var redis = new RedisService("localhost:6379");
                redis.RunDemonstration();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка підключення до Redis: {ex.Message}");
            }

            /*Console.WriteLine("Connecting to the database...");

            try
            {
                using (var unitOfWork = new UnitOfWork(connectionString))
                {
                    var newClient = new Client { CompanyName = "Logistics Plus LLC", ContactPhone = "+380501234567" };
                    unitOfWork.Clients.Add(newClient, null);
                    Console.WriteLine("New client successfully added to the transaction.");

                    var vehicles = unitOfWork.Vehicles.GetActiveVehicles();
                    Console.WriteLine($"\nFound active vehicles: {vehicles.Count}");
                    foreach (var v in vehicles)
                    {
                        Console.WriteLine($"- Vehicle: {v.Make} {v.Model} (Plate: {v.LicensePlate})");
                    }

                    unitOfWork.Commit();
                    Console.WriteLine("\nAll changes successfully saved to the database (Commit)!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
            }*/

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}