using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using UltraHardcoreAssistent.Bot;

namespace UltraHardcoreAssistent.UI
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private GameBot bot;
        private ObservableCollection<string> enteredWordsList = new ObservableCollection<string>();
        private ObservableCollection<string> pressedArrowsList = new ObservableCollection<string>();
        private int totalArrows = 0;
        private int totalWords = 0;

        public MainWindow()
        {
            InitializeComponent();
            bot = new GameBot();
        }

        private void AddArrowsToList(string m)
        {
            totalArrows++;
            lbTotalArrows.Content = totalArrows.ToString();
            pressedArrowsList.Add(m);
            if (pressedArrowsList.Count > 100)
            {
                for (int i = 0; i < pressedArrowsList.Count - 100; i++)
                {
                    pressedArrowsList.RemoveAt(0);
                }
            }
        }

        private void AddWordToList(string m)
        {
            totalWords++;
            lbTotalWords.Content = totalWords.ToString();
            enteredWordsList.Add(m);
            if (enteredWordsList.Count > 100)
            {
                for (int i = 0; i < enteredWordsList.Count - 100; i++)
                {
                    enteredWordsList.RemoveAt(0);
                }
            }
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            //enteredWordsList.Add("Привет");
            if (bot.IsWork == false)
            {
                bot.StartWorkAsync();
                btnStartText.Content = "STOP";
                btnStart.Background = new SolidColorBrush(new Color() { A = 80, R = 128, G = 0, B = 0 });
            }
            else
            {
                bot.StopWorkAsync();
                btnStartText.Content = "START";
                btnStart.Background = new SolidColorBrush(new Color() { A = 80, R = 0, G = 128, B = 0 });
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bot.StopWorkAsync();
            bot = null;
            GC.Collect();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            lbTotalArrows.DataContext = totalArrows;
            lbTotalWords.DataContext = totalWords;

            lsbWords.ItemsSource = enteredWordsList;
            lsbArrows.ItemsSource = pressedArrowsList;
            bot.OnTextEntered += (m) =>
            {
                Dispatcher.Invoke(() =>
                {
                    AddWordToList(m);
                });
            };
            bot.OnArrowsPressed += (m) =>
            {
                Dispatcher.Invoke(() =>
                {
                    AddArrowsToList(m);
                });
            };
        }
    }
}