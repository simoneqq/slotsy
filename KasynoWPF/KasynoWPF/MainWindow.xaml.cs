using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Threading.Tasks;

namespace KasynoWPF
{
    public partial class MainWindow : Window
    {
        private readonly string[] _symbols = { "777", "BAR", "CHERRY", "LEMON", "GRAPE" };
        private readonly Random _random = new Random();
        private Dictionary<int, StackPanel> _drums;

        private const int RowCount = 5;
        private const int ColCount = 5;
        private const double CellHeight = 75;
        private const int FakeSymbolsCount = 35;

        // ═══════════════════════════════════════════════════════════
        // WIRTUALNE PIENIĄDZE I STAWKA
        // ═══════════════════════════════════════════════════════════
        private int _money = 1000;
        private int _bet = 21;          // 1 moneta × 21 linii = domyślna stawka
        private const int BetStep = 21; // Krok = 1 moneta na linię
        private const int MinBet = 21;  // Min: 1/linię
        private const int MaxBet = 2100; // Max: 100/linię

        // ═══════════════════════════════════════════════════════════
        // TABELA MNOŻNIKÓW – KALIBRACJA RTP ≈ 98%
        //
        // Wypłata za linię = multiplier × (totalBet / 21)
        //
        // Kalibracja: RTP = avg_m3×P(3) + avg_m4×P(4) + avg_m5×P(5)
        //   avg_m3 = (20+10+6+3+1)/5 = 8.0
        //   avg_m4 = (100+50+30+15+5)/5 = 40.0
        //   avg_m5 = (800+350+200+100+30)/5 = 296
        //   RTP = 8.0×0.032 + 40.0×0.0064 + 296×0.0016 ≈ 0.986 ≈ 98%
        // ═══════════════════════════════════════════════════════════
        private readonly Dictionary<string, int[]> _multipliers = new Dictionary<string, int[]>
        {
            // Symbol   → [ 3-z-rzędu, 4-z-rzędu, 5-z-rzędu ] × stawka/linię
            { "777",    new[] {  20,  100,  800 } },
            { "BAR",    new[] {  10,   50,  350 } },
            { "CHERRY", new[] {   6,   30,  200 } },
            { "LEMON",  new[] {   3,   15,  100 } },
            { "GRAPE",  new[] {   1,    5,   30 } },
        };

        public MainWindow()
        {
            InitializeComponent();
            _drums = new Dictionary<int, StackPanel>
            {
                { 0, Drum1 }, { 1, Drum2 }, { 2, Drum3 }, { 3, Drum4 }, { 4, Drum5 }
            };
            InitializeSlots();
            UpdateUI();
        }

        // ═══════════════════════════════════════════════════════════
        // AKTUALIZACJA INTERFEJSU
        // ═══════════════════════════════════════════════════════════
        private void UpdateUI(string statusMessage = null)
        {
            MoneyLabel.Text = $"💰  {_money:N0} monet";
            BetLabel.Text = $"🎲  Stawka: {_bet} monet  ({_bet / BetStep}/linię × 21)";

            if (statusMessage != null)
                StatusLabel.Text = statusMessage;

            // Tryb „bankrut" – zamień SPIN na RESET
            if (_money < MinBet)
            {
                StatusLabel.Text = "💸 Brak monet! Kliknij RESET aby zagrać ponownie.";
                SpinButton.Content = "RESET";
                SpinButton.IsEnabled = true;
                BetDownButton.IsEnabled = false;
                BetUpButton.IsEnabled = false;
            }
            else
            {
                SpinButton.Content = "SPIN!";
                SpinButton.IsEnabled = (_money >= _bet);
                BetDownButton.IsEnabled = (_bet > MinBet);
                BetUpButton.IsEnabled = (_bet < MaxBet);
            }
        }

        private void BetUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (_bet < MaxBet) { _bet += BetStep; UpdateUI(); }
        }

        private void BetDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (_bet > MinBet) { _bet -= BetStep; UpdateUI(); }
        }

        // ═══════════════════════════════════════════════════════════
        // INICJALIZACJA BĘBNÓW
        // ═══════════════════════════════════════════════════════════
        private void InitializeSlots()
        {
            for (int c = 0; c < ColCount; c++)
            {
                _drums[c].Children.Clear();
                for (int r = 0; r < RowCount; r++)
                    _drums[c].Children.Add(CreateSymbolCell(_symbols[_random.Next(_symbols.Length)]));
            }
        }

        private Border CreateSymbolCell(string symbol)
        {
            Border border = new Border
            {
                Width = 90,
                Height = CellHeight,
                BorderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 55)),
                BorderThickness = new Thickness(1),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new TransformGroup
                {
                    Children = new TransformCollection { new ScaleTransform(1, 1) }
                }
            };

            Brush textBrush = Brushes.White;
            Brush bgBrush = new SolidColorBrush(Color.FromRgb(25, 25, 25));

            switch (symbol)
            {
                case "777":
                    textBrush = new SolidColorBrush(Color.FromRgb(255, 215, 0));
                    bgBrush = new SolidColorBrush(Color.FromRgb(70, 15, 15));
                    break;
                case "BAR":
                    textBrush = new SolidColorBrush(Color.FromRgb(0, 255, 255));
                    bgBrush = new SolidColorBrush(Color.FromRgb(15, 40, 65));
                    break;
                case "CHERRY":
                    textBrush = new SolidColorBrush(Color.FromRgb(255, 70, 70));
                    break;
                case "LEMON":
                    textBrush = new SolidColorBrush(Color.FromRgb(255, 255, 0));
                    break;
                case "GRAPE":
                    textBrush = new SolidColorBrush(Color.FromRgb(190, 80, 255));
                    break;
            }

            border.Background = bgBrush;

            Viewbox viewbox = new Viewbox { Stretch = Stretch.Uniform, Margin = new Thickness(8) };
            TextBlock textBlock = new TextBlock
            {
                Text = symbol,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Arial Black"),
                Foreground = textBrush,
                TextAlignment = TextAlignment.Center
            };
            viewbox.Child = textBlock;
            border.Child = viewbox;
            return border;
        }

        // ═══════════════════════════════════════════════════════════
        // SPIN – KLIKNIĘCIE
        // ═══════════════════════════════════════════════════════════
        private async void SpinButton_Click(object sender, RoutedEventArgs e)
        {
            // Tryb RESET – doładuj konto
            if (_money < MinBet)
            {
                _money = 1000;
                _bet = MinBet;
                UpdateUI("🎁 Saldo uzupełnione do 1000 monet. Powodzenia!");
                return;
            }

            // Pobierz stawkę przed spinem
            _money -= _bet;

            // Zablokuj przyciski na czas animacji
            SpinButton.IsEnabled = false;
            BetUpButton.IsEnabled = false;
            BetDownButton.IsEnabled = false;
            MoneyLabel.Text = $"💰  {_money:N0} monet";
            StatusLabel.Text = "🎰 Losowanie...";

            // Losuj finalną macierz wyników
            string[,] finalMatrix = new string[ColCount, RowCount];
            for (int c = 0; c < ColCount; c++)
                for (int r = 0; r < RowCount; r++)
                    finalMatrix[c, r] = _symbols[_random.Next(_symbols.Length)];

            int animationDurationMs = 1200;

            // Przygotuj bębny do animacji
            for (int c = 0; c < ColCount; c++)
            {
                var drum = _drums[c];

                List<string> currentVisibleSymbols = new List<string>();
                int childCount = drum.Children.Count;
                for (int r = 0; r < RowCount; r++)
                {
                    int index = childCount - RowCount + r;
                    if (index >= 0 && drum.Children[index] is Border b
                        && b.Child is Viewbox vb && vb.Child is TextBlock tb)
                        currentVisibleSymbols.Add(tb.Text);
                    else
                        currentVisibleSymbols.Add(_symbols[_random.Next(_symbols.Length)]);
                }

                if (drum.RenderTransform is TranslateTransform oldT)
                {
                    oldT.BeginAnimation(TranslateTransform.YProperty, null);
                    oldT.Y = 0;
                }

                drum.Children.Clear();

                // 1. Bieżące (widoczne) symbole – ciągłość wizualna
                for (int r = 0; r < RowCount; r++)
                    drum.Children.Add(CreateSymbolCell(currentVisibleSymbols[r]));

                // 2. Klocki przelotowe
                for (int i = 0; i < FakeSymbolsCount - RowCount; i++)
                    drum.Children.Add(CreateSymbolCell(_symbols[_random.Next(_symbols.Length)]));

                // 3. Finalny wynik na dole taśmy
                for (int r = 0; r < RowCount; r++)
                    drum.Children.Add(CreateSymbolCell(finalMatrix[c, r]));

                drum.UpdateLayout();
            }

            // Odpal animacje kaskadowo
            for (int c = 0; c < ColCount; c++)
            {
                var drum = _drums[c];
                if (drum.RenderTransform is TranslateTransform transform)
                {
                    double targetY = -(FakeSymbolsCount * CellHeight);
                    DoubleAnimation da = new DoubleAnimation
                    {
                        From = 0,
                        To = targetY,
                        Duration = TimeSpan.FromMilliseconds(animationDurationMs),
                        EasingFunction = new BackEase { Amplitude = 0.12, EasingMode = EasingMode.EaseOut }
                    };
                    transform.BeginAnimation(TranslateTransform.YProperty, da);
                }
                await Task.Delay(80);
            }

            await Task.Delay(animationDurationMs + (ColCount * 80));

            // Sprawdź wygraną i zaktualizuj saldo
            int totalWin = CheckAndAnimateWins(finalMatrix);
            if (totalWin > 0)
            {
                _money += totalWin;
                UpdateUI($"🔥 WYGRANA!  +{totalWin} monet!");
            }
            else
            {
                UpdateUI("😞 Brak układu. Spróbuj ponownie!");
            }
        }

        // ═══════════════════════════════════════════════════════════
        // SPRAWDZANIE WYGRANEJ
        // Zwraca łączną kwotę wygranej (0 = brak).
        // ═══════════════════════════════════════════════════════════
        private int CheckAndAnimateWins(string[,] matrix)
        {
            List<int[]> paylines = new List<int[]>
            {
                // Poziome
                new int[] { 0,0,0,0,0 }, new int[] { 1,1,1,1,1 }, new int[] { 2,2,2,2,2 },
                new int[] { 3,3,3,3,3 }, new int[] { 4,4,4,4,4 },
                // Przekątne
                new int[] { 0,1,2,3,4 }, new int[] { 4,3,2,1,0 },
                // V-kształty
                new int[] { 0,1,2,1,0 }, new int[] { 4,3,2,3,4 },
                new int[] { 1,2,3,2,1 }, new int[] { 3,2,1,2,3 },
                // Zygzaki
                new int[] { 0,2,0,2,0 }, new int[] { 4,2,4,2,4 },
                new int[] { 1,3,1,3,1 }, new int[] { 3,1,3,1,3 },
                // Schodki/fale
                new int[] { 0,0,1,2,2 }, new int[] { 2,2,1,0,0 },
                new int[] { 2,2,3,4,4 }, new int[] { 4,4,3,2,2 },
                new int[] { 1,1,2,3,3 }, new int[] { 3,3,2,1,1 }
            };

            int lineBet = _bet / BetStep; // Monety na jedną linię wypłatną
            int totalWin = 0;
            HashSet<(int col, int row)> winningCells = new HashSet<(int, int)>();

            foreach (var line in paylines)
            {
                string firstSymbol = matrix[0, line[0]];
                int matchCount = 1;

                for (int c = 1; c < ColCount; c++)
                {
                    if (matrix[c, line[c]] == firstSymbol) matchCount++;
                    else break;
                }

                if (matchCount >= 3)
                {
                    // Mnożnik zależy od symbolu i długości dopasowania
                    int multiplier = _multipliers[firstSymbol][matchCount - 3];
                    int lineWin = multiplier * lineBet;
                    totalWin += lineWin;

                    for (int c = 0; c < matchCount; c++)
                        winningCells.Add((c, line[c]));
                }
            }

            foreach (var cell in winningCells)
                AnimateWinningCell(cell.col, cell.row);

            return totalWin;
        }

        private void AnimateWinningCell(int col, int row)
        {
            var drum = _drums[col];
            int visualIndex = FakeSymbolsCount + row;

            if (drum.Children[visualIndex] is Border border)
            {
                var tg = border.RenderTransform as TransformGroup;
                var scale = tg?.Children[0] as ScaleTransform;
                if (scale != null)
                {
                    var anim = new DoubleAnimation
                    {
                        From = 1.0,
                        To = 1.15,
                        Duration = TimeSpan.FromMilliseconds(250),
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever
                    };
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
                }
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 215, 0));
                border.BorderThickness = new Thickness(3);
            }
        }
    }
}