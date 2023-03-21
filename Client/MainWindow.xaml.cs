using CarServerLib;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Client;

public partial class MainWindow : Window
{
    #region DependencyProperties

    public static readonly DependencyProperty CarProperty =
DependencyProperty.Register("Car", typeof(Car), typeof(MainWindow));
    public static readonly DependencyProperty IsTextBoxEnabledProperty =
    DependencyProperty.Register("IsTextBoxEnabled", typeof(bool), typeof(MainWindow));


    public Car Car
    {
        get { return (Car)GetValue(CarProperty); }
        set { SetValue(CarProperty, value); }
    }

    public bool IsTextBoxEnabled
    {
        get { return (bool)GetValue(IsTextBoxEnabledProperty); }
        set { SetValue(IsTextBoxEnabledProperty, value); }
    }
    #endregion

    Command cmd;
    private TcpClient client;
    public ObservableCollection<Car> Cars { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        client = new TcpClient("127.0.0.1", 12345);
        IsTextBoxEnabled = false;
        Car = new();
        Cars = new();
        cmd = new Command();
        cmbCommand.SelectedIndex = 0;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) =>
        cmbCommand.ItemsSource = Enum.GetValues(typeof(HttpMethods)).Cast<HttpMethods>();


    private void BtnRequest_Click(object sender, RoutedEventArgs e)
    {
        if (cmbCommand.SelectedItem is HttpMethods method)
            SendRequest(method);
    }

    private void cmbCommand_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbCommand.SelectedItem is HttpMethods method)
        {
            cmd.Method = method;

            switch (method)
            {
                case HttpMethods.GET:
                case HttpMethods.DELETE:
                    requestGrid.Children.OfType<TextBox>().Where(t => t != txtId).ToList()
                      .ForEach(txt => txt.Text = string.Empty);
                    IsTextBoxEnabled = false;
                    break;
                case HttpMethods.POST:
                case HttpMethods.PUT:
                    IsTextBoxEnabled = true;
                    break;
            }

        }
    }



    private async void SendRequest(HttpMethods method)
    {
        var stream = client.GetStream();
        var bw = new BinaryWriter(stream);
        var br = new BinaryReader(stream);

        switch (method)
        {
            case HttpMethods.GET:
                {
                    if (Car.Id < 0)
                    {
                        MessageBox.Show("Id is invalid!");
                        return;
                    }

                    cmd.Car = Car;
                    var request = JsonSerializer.Serialize(cmd);
                    bw.Write(request);

                    await Task.Delay(60);

                    if (Car.Id == 0)
                    {
                        var allCars = br.ReadString();
                        var cars = JsonSerializer.Deserialize<List<Car>>(allCars);

                        Cars.Clear();

                        foreach (var c in cars)
                            Cars.Add(c);

                        return;
                    }

                    var response = br.ReadString();

                    var car = JsonSerializer.Deserialize<Car>(response);

                    if (car != null)
                    {
                        Cars.Clear();
                        Cars.Add(car);
                    }
                    else
                    {
                        MessageBox.Show($"Car not found with this '{Car.Id}' Id");
                        Cars.Clear();
                    }

                    break;
                }


            case HttpMethods.POST:
                {
                    var sb = CheckValidation();

                    if (sb.Length > 0)
                    {
                        MessageBox.Show(sb.ToString());
                        return;
                    }

                    cmd.Car = Car;
                    var request = JsonSerializer.Serialize(cmd);

                    bw.Write(request);

                    await Task.Delay(60);


                    var isPosted = br.ReadBoolean();
                    
                    var msg = isPosted ? "Posted successfully" : $"Car already exists with Id '{Car.Id}'!";
                    
                    MessageBox.Show(msg);
                   
                    Cars.Clear();

                    break;
                }
            case HttpMethods.PUT:
                {
                    StringBuilder sb = CheckValidation();

                    if (sb.Length > 0)
                    {
                        MessageBox.Show(sb.ToString());
                        return;
                    }

                    cmd.Car = Car;
                    var request = JsonSerializer.Serialize(cmd);


                    bw.Write(request);

                    await Task.Delay(60);

                    var isPosted = br.ReadBoolean();
                    
                    var msg = isPosted ? "Updated successfully!" : $"Car is not exist with this '{Car.Id}' Id!";

                    MessageBox.Show(msg);
                    Cars.Clear();

                    break;
                }
            case HttpMethods.DELETE:
                {
                    if (Car.Id <= 0)
                    {
                        MessageBox.Show("Entered id is invalid");
                        return;
                    }

                    cmd.Car = Car;
                    var request = JsonSerializer.Serialize(cmd);

                    bw.Write(request);

                    await Task.Delay(60);

                    var isDeleted = br.ReadBoolean();            
                    var msg = isDeleted ? "Deleted succesfully!" : $"Car not found with this '{Car.Id}' Id!";
                    MessageBox.Show(msg);

                    Cars.Clear();
                    break;
                }
        }
    }

    private StringBuilder CheckValidation()
    {
        var sb = new StringBuilder();
        var validYear = 1970;

        if (Car.Id <= 0)
            sb.Append("Id is invalid!");

        if (Car.Year < validYear || Car.Year > DateTime.Now.Year)
            sb.Append($"Year is invalid! (Valid Year: {validYear} and upper)");

        if (string.IsNullOrWhiteSpace(Car.Make)
            || string.IsNullOrWhiteSpace(Car.Model)
            || string.IsNullOrWhiteSpace(Car.VIN)
            || string.IsNullOrEmpty(Car.Color))
            sb.Append("Please complete all required information!");

        return sb;
    }
}
