using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
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
using MoexIssAPI;
using MoexIssAPI.Requests;

namespace Moex
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {

            InitializeComponent();

            DataContext = this;

            Refresh();

        }



        public ObservableCollection<Item> Engines { get; set; } = new ObservableCollection<Item>();
        public ObservableCollection<Item> Markets { get; set; } = new ObservableCollection<Item>();
        public ObservableCollection<Item> Securities { get; set; } = new ObservableCollection<Item>();
        public ObservableCollection<Item> Boards { get; set; } = new ObservableCollection<Item>();

        public class HistoryObject
        {
            public DateTime Date { get; set; }
            public decimal Value { get; set; }
            public string Board;
        }

        public string Info { get; set; }
        public List<HistoryObject> History { get; set; }


        public Item Security { get; set; }
        public Item Engine { get; set; }
        public Item Market { get; set; }
        public Item Board { get; set; }

        public string SecText { get; set; }

        public class Item
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public Dictionary<string, string> Data;
        }


        public event PropertyChangedEventHandler PropertyChanged;

        void FirePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        void Refresh()
        {
            try
            {

                Engines.Clear();

                foreach (var item in new EnginesRequest().Response.Engines.Data.Select(d => new Item()
                {
                    Name = d["title"],
                    Value = d["name"],
                    Data =d
                }))
                   Engines.Add(item);


                Markets.Clear();
                Securities.Clear();
                Boards.Clear();

                Info = "";
                History = new List<HistoryObject>();

                FirePropertyChanged(nameof(Info));
                FirePropertyChanged(nameof(History));


            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error");
            }

            
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            
            if (!string.IsNullOrEmpty(SecText))
            {
                try
                {

                    var symbol = SecText.Split(' ')[0];

                    History = await Task.Run(()=> new SecurityHistoryRequest(Engine.Value, Market.Value, Board.Value, symbol).Response.Object.Data.Where(d => d["CLOSE"] != null).Select(d =>
                        new HistoryObject()
                        {
                            Date = DateTime.Parse(d["TRADEDATE"]),
                            Board = d["BOARDID"],
                            Value = Market.Value.Equals("bonds") ?
                              decimal.Parse(d["CLOSE"], System.Globalization.CultureInfo.InvariantCulture) * decimal.Parse(d["FACEVALUE"], System.Globalization.CultureInfo.InvariantCulture) /100 +
                               decimal.Parse(d["ACCINT"], System.Globalization.CultureInfo.InvariantCulture) :
                              decimal.Parse(d["CLOSE"], System.Globalization.CultureInfo.InvariantCulture)
                        }
                    ).ToList());


                    FirePropertyChanged(nameof(History));

                    if (History.Count > 0)
                    {
                        StringBuilder SQL = new StringBuilder();
                        SQL.AppendLine($"DECLARE @Id INT = (SELECT Id from Securities where Symbol='{symbol}' and Board='{History[0].Board}');\n");
                        SQL.AppendLine($"INSERT INTO EndOfDay (SecurityId, Date, Value) VALUES");
                        SQL.AppendLine(string.Join("\n", History.Select(s => $"(@Id,{s.Date.Year*10000+s.Date.Month*100+s.Date.Day},{s.Value}),")));
                        SQL.AppendLine(";");

                        File.WriteAllText($"{symbol}.sql", SQL.ToString());
                    }

                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error");
                }
            }

        }


        private void Engine_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {

                Markets.Clear();
                foreach (var item in new MarketsRequest(Engine.Value).Response.Markets.Data.Select(d =>
                new Item
                {
                    Name = d["title"],
                    Value = d["NAME"],
                    Data = d
                }
                ))
                    Markets.Add(item);

                Securities.Clear();
                Boards.Clear();

                Info = "";
                History = new List<HistoryObject>();
                FirePropertyChanged(nameof(Info));
                FirePropertyChanged(nameof(History));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }
        }
        class ItemEqvCmp : IEqualityComparer<Item>
        {
            public bool Equals(Item x, Item y)
            {
                return x.Name == y.Name && x.Value == y.Value;
            }

            public int GetHashCode(Item obj)
            {
                return obj.Name.GetHashCode() + obj.Value.GetHashCode();
            }
        }
        private async void Market_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Securities.Clear();
            Boards.Clear();

            Info = "";
            History = new List<HistoryObject>();
            FirePropertyChanged(nameof(Info));
            FirePropertyChanged(nameof(History));

            try
            {
                foreach (var item in 
                await Task<IEnumerable<Item>>.Run(() =>
                {
                  return new MarketSecuritiesListRequest(Engine.Value, Market.Value).Response.Securities.Data.Select(d =>
                  new Item
                  {
                    Name = $"{d["SECID"]}",
                    Value = d["SECID"],
                    Data = d
                  }).Distinct(new ItemEqvCmp());

                }))
                    Securities.Add(item);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
            }


        }

        private void Security_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SecChanged();
        }

        async void SecChanged()
        {

            Boards.Clear();

            var symbol = (Security?.Name?? SecText).Split(' ')[0];

            try
            {
                var resp = await Task.Run((() => new SecurityDefinitionRequest(symbol).Response));


                Info =
                       string.Join("\n", resp.Description.Data.Select(d => $"{d["name"]}: {d["value"]}"));

                foreach (var board in resp.Boards.Data)
                    Boards.Add(new Item()
                    {
                        Name = board["boardid"],
                        Value = board["boardid"],
                        Data = board
                    });

            }
            catch (Exception e)
            {
                Info = "";
            }

            FirePropertyChanged(nameof(Info));


            

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Refresh();
        }


        private void ComboBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SecChanged();

        }
    }
}
