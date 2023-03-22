using System.Net.Http;
using System.Net.Sockets;
using System.Net;
using System.Text.Json;
using CarServerLib;
using System.Collections.Concurrent;

List<Car> cars = null!;

if (File.Exists(@"..\..\..\Cars.json"))
{
    var jsonCars = File.ReadAllText(@"..\..\..\Cars.json");
    cars = JsonSerializer.Deserialize<List<Car>>(jsonCars)!;
}

if (cars is null)
    cars = new();

var listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 12345);

listener.Start(10);

while (true)
{
    var client = await listener.AcceptTcpClientAsync();
    var sync = new object();

    Console.WriteLine($"Client {client.Client.RemoteEndPoint} accepted");

    await Task.Run(() =>
    {
        var stream = client.GetStream();
        var bw = new BinaryWriter(stream);
        var br = new BinaryReader(stream);

        while (true)
        {
            var request = br.ReadString();

            var command = JsonSerializer.Deserialize<Command>(request);

            if (command is null)
                continue;

            switch (command.Method)
            {
                case HttpMethods.GET:
                    {
                        var id = command.Car?.Id;
                        if (id == 0)
                        {
                            var result = JsonSerializer.Serialize(cars);
                            bw.Write(result);
                            break;
                        }

                        Car? car = null;
                        cars.FindAll(c => c.Id == id);
                        car = cars.FirstOrDefault(c => c.Id == id);

                        var response = JsonSerializer.Serialize(car);
                        bw.Write(response);
                        break;
                    }
                case HttpMethods.POST:
                    {
                        var id = command.Car?.Id;
                        var canPosted = !cars.Any(c => c.Id == id);

                        if (canPosted)
                        {
                            if (command.Car is not null)
                            {
                                lock (sync)
                                {
                                    cars.Add(command.Car);
                                }
                            }
                        }

                        bw.Write(canPosted);

                        break;
                    }
                case HttpMethods.PUT:
                    {
                        var id = command.Car?.Id;
                        var carIndex = cars.FindIndex(c => c.Id == id);
                        var canPut = carIndex >= 0;
                        if (canPut)
                        {
                            lock (sync)
                            {
                                cars[carIndex] = command.Car!;
                            }
                        }
                        bw.Write(canPut);
                        break;
                    }

                case HttpMethods.DELETE:
                    {
                        var isDeleted = false;
                        var id = command.Car?.Id;
                        var carToRemove = cars.FirstOrDefault(c => c.Id == id);

                        if (carToRemove != null)
                        {
                            lock (sync)
                            {
                                cars.Remove(carToRemove);
                            }
                            isDeleted = true;
                        }
                        bw.Write(isDeleted);
                        break;
                    }
            }

            SaveCarsToFile(cars, sync);
        }
    });
}

static void SaveCarsToFile(List<Car> cars, object sync)
{
    lock (sync)
    {
        var jsonCars = JsonSerializer.Serialize(cars);
        File.WriteAllTextAsync(@"..\..\..\Cars.json", jsonCars);
    }
}