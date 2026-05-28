using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Threading.Tasks;

namespace KasynoWPF
{
    // Model ostatnich wyników ruletki (ItemsControl binding)
    public class RouletteHistoryItem
    {
        public string Number { get; set; }
        public Brush Color { get; set; }
    }

    public partial class MainWindow : Window
    {
        // ════════════════════════════════════════════════════════
        // SLOTY – pola
        // ════════════════════════════════════════════════════════
        private readonly string[] _symbols = { "777", "BAR", "CHERRY", "LEMON", "GRAPE" };
        private readonly Random _random = new Random();
        private Dictionary<int, StackPanel> _drums;

        private const int RowCount = 5;
        private const int ColCount = 5;
        private const double CellHeight = 75;
        private const int FakeSymbolsCount = 35;

        private int _money = 1000;
        private int _bet = 21;
        private const int BetStep = 21;
        private const int MinBet = 21;
        private const int MaxBet = 2100;

        private bool _slotSpinning = false;

        private readonly Dictionary<string, int[]> _multipliers = new Dictionary<string, int[]>
        {
            { "777",    new[] {  20, 100, 800 } },
            { "BAR",    new[] {  10,  50, 350 } },
            { "CHERRY", new[] {   6,  30, 200 } },
            { "LEMON",  new[] {   3,  15, 100 } },
            { "GRAPE",  new[] {   1,   5,  30 } },
        };

        // ════════════════════════════════════════════════════════
        // RULETKA – pola
        // ════════════════════════════════════════════════════════
        private readonly Dictionary<string, int> _rouletteBets = new Dictionary<string, int>();
        private readonly Dictionary<string, Button> _betButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, string> _betLabels = new Dictionary<string, string>();
        private readonly Dictionary<string, SolidColorBrush> _betOriginalBg = new Dictionary<string, SolidColorBrush>();
        private readonly ObservableCollection<RouletteHistoryItem> _rouletteHistory
                                                                              = new ObservableCollection<RouletteHistoryItem>();

        private int _selectedChip = 10;
        private bool _rouletteBuilt = false;
        private bool _rouletteSpinning = false;

        // Numery czerwone w europejskiej ruletce
        private static readonly HashSet<int> RedNums =
            new HashSet<int> { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 };

        // Kolory żetonów (normalny / wybrany)
        private static readonly (Color normal, Color active)[] ChipColors =
        {
            (Color.FromRgb(122,0,0),   Color.FromRgb(220,50,50)),   // 1
            (Color.FromRgb(26,84,26),  Color.FromRgb(50,170,50)),   // 5
            (Color.FromRgb(26,26,122), Color.FromRgb(60,60,230)),   // 10
            (Color.FromRgb(90,56,0),   Color.FromRgb(180,110,0)),   // 25
            (Color.FromRgb(58,0,80),   Color.FromRgb(140,0,190)),   // 100
            (Color.FromRgb(0,58,58),   Color.FromRgb(0,140,140)),   // 500
        };

        // ════════════════════════════════════════════════════════
        // KONSTRUKTOR
        // ════════════════════════════════════════════════════════
        public MainWindow()
        {
            InitializeComponent();

            _drums = new Dictionary<int, StackPanel>
            {
                { 0, Drum1 }, { 1, Drum2 }, { 2, Drum3 }, { 3, Drum4 }, { 4, Drum5 }
            };
            InitializeSlots();
            UpdateUI();

            LastResults.ItemsSource = _rouletteHistory;
            HighlightChip(ChipBtn10); // domyślny żeton 10
        }

        // ════════════════════════════════════════════════════════
        // NAWIGACJA – helper fade
        // ════════════════════════════════════════════════════════
        private void NavigateTo(UIElement from, UIElement to)
        {
            var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            fadeOut.Completed += (_, __) =>
            {
                from.Visibility = Visibility.Collapsed;
                to.Opacity = 0;
                to.Visibility = Visibility.Visible;
                to.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(320)));
            };
            from.BeginAnimation(OpacityProperty, fadeOut);
        }

        // Ekran startowy → Sloty
        private void SlotCard_Click(object sender, MouseButtonEventArgs e)
        {
            UpdateUI();
            NavigateTo(StartScreen, GameGrid);
        }

        // Ekran startowy → Ruletka
        private void RouletteCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (!_rouletteBuilt) { BuildRouletteTable(); _rouletteBuilt = true; }
            RefreshRouletteHeader();
            NavigateTo(StartScreen, RouletteGrid);
        }

        // Sloty → ekran startowy
        private void SlotBackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_slotSpinning) return;
            NavigateTo(GameGrid, StartScreen);
        }

        // Ruletka → ekran startowy
        private void RouletteBackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_rouletteSpinning) return;
            NavigateTo(RouletteGrid, StartScreen);
        }

        // ════════════════════════════════════════════════════════
        // SLOTY – logika (bez zmian)
        // ════════════════════════════════════════════════════════
        private void UpdateUI(string msg = null)
        {
            MoneyLabel.Text = $"💰  {_money:N0} monet";
            BetLabel.Text = $"🎲  Stawka: {_bet} monet  ({_bet / BetStep}/linię × 21)";
            if (msg != null) StatusLabel.Text = msg;

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
        { if (_bet < MaxBet) { _bet += BetStep; UpdateUI(); } }

        private void BetDownButton_Click(object sender, RoutedEventArgs e)
        { if (_bet > MinBet) { _bet -= BetStep; UpdateUI(); } }

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
            var border = new Border
            {
                Width = 90,
                Height = CellHeight,
                BorderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 55)),
                BorderThickness = new Thickness(1),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new TransformGroup
                { Children = new TransformCollection { new ScaleTransform(1, 1) } }
            };

            Brush txt = Brushes.White;
            Brush bg = new SolidColorBrush(Color.FromRgb(25, 25, 25));
            switch (symbol)
            {
                case "777": txt = new SolidColorBrush(Color.FromRgb(255, 215, 0)); bg = new SolidColorBrush(Color.FromRgb(70, 15, 15)); break;
                case "BAR": txt = new SolidColorBrush(Color.FromRgb(0, 255, 255)); bg = new SolidColorBrush(Color.FromRgb(15, 40, 65)); break;
                case "CHERRY": txt = new SolidColorBrush(Color.FromRgb(255, 70, 70)); break;
                case "LEMON": txt = new SolidColorBrush(Color.FromRgb(255, 255, 0)); break;
                case "GRAPE": txt = new SolidColorBrush(Color.FromRgb(190, 80, 255)); break;
            }
            border.Background = bg;

            var vb = new Viewbox { Stretch = Stretch.Uniform, Margin = new Thickness(8) };
            vb.Child = new TextBlock
            {
                Text = symbol,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Arial Black"),
                Foreground = txt,
                TextAlignment = TextAlignment.Center
            };
            border.Child = vb;
            return border;
        }

        private async void SpinButton_Click(object sender, RoutedEventArgs e)
        {
            if (_money < MinBet)
            {
                _money = 1000; _bet = MinBet;
                UpdateUI("🎁 Saldo uzupełnione do 1000 monet. Powodzenia!");
                return;
            }

            _money -= _bet;
            _slotSpinning = true;
            SpinButton.IsEnabled = false;
            BetUpButton.IsEnabled = false;
            BetDownButton.IsEnabled = false;
            SlotBackButton.IsEnabled = false;
            MoneyLabel.Text = $"💰  {_money:N0} monet";
            StatusLabel.Text = "🎰 Losowanie...";

            var final = new string[ColCount, RowCount];
            for (int c = 0; c < ColCount; c++)
                for (int r = 0; r < RowCount; r++)
                    final[c, r] = _symbols[_random.Next(_symbols.Length)];

            const int animMs = 1200;

            for (int c = 0; c < ColCount; c++)
            {
                var drum = _drums[c];
                var vis = new List<string>();
                int cnt = drum.Children.Count;
                for (int r = 0; r < RowCount; r++)
                {
                    int idx = cnt - RowCount + r;
                    vis.Add(idx >= 0 && drum.Children[idx] is Border b
                        && b.Child is Viewbox vb && vb.Child is TextBlock tb
                        ? tb.Text : _symbols[_random.Next(_symbols.Length)]);
                }

                if (drum.RenderTransform is TranslateTransform old)
                { old.BeginAnimation(TranslateTransform.YProperty, null); old.Y = 0; }

                drum.Children.Clear();
                foreach (var s in vis) drum.Children.Add(CreateSymbolCell(s));
                for (int i = 0; i < FakeSymbolsCount - RowCount; i++) drum.Children.Add(CreateSymbolCell(_symbols[_random.Next(_symbols.Length)]));
                for (int r = 0; r < RowCount; r++) drum.Children.Add(CreateSymbolCell(final[c, r]));
                drum.UpdateLayout();
            }

            for (int c = 0; c < ColCount; c++)
            {
                if (_drums[c].RenderTransform is TranslateTransform t)
                    t.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
                    {
                        From = 0,
                        To = -(FakeSymbolsCount * CellHeight),
                        Duration = TimeSpan.FromMilliseconds(animMs),
                        EasingFunction = new BackEase { Amplitude = 0.12, EasingMode = EasingMode.EaseOut }
                    });
                await Task.Delay(80);
            }
            await Task.Delay(animMs + ColCount * 80);

            int win = CheckAndAnimateWins(final);
            if (win > 0) { _money += win; UpdateUI($"🔥 WYGRANA!  +{win} monet!"); }
            else UpdateUI("😞 Brak układu. Spróbuj ponownie!");

            _slotSpinning = false;
            SlotBackButton.IsEnabled = true;
        }

        private int CheckAndAnimateWins(string[,] matrix)
        {
            var paylines = new List<int[]>
            {
                new[]{0,0,0,0,0},new[]{1,1,1,1,1},new[]{2,2,2,2,2},new[]{3,3,3,3,3},new[]{4,4,4,4,4},
                new[]{0,1,2,3,4},new[]{4,3,2,1,0},
                new[]{0,1,2,1,0},new[]{4,3,2,3,4},new[]{1,2,3,2,1},new[]{3,2,1,2,3},
                new[]{0,2,0,2,0},new[]{4,2,4,2,4},new[]{1,3,1,3,1},new[]{3,1,3,1,3},
                new[]{0,0,1,2,2},new[]{2,2,1,0,0},new[]{2,2,3,4,4},new[]{4,4,3,2,2},
                new[]{1,1,2,3,3},new[]{3,3,2,1,1}
            };

            int lineBet = _bet / BetStep, totalWin = 0;
            var winCells = new HashSet<(int, int)>();

            foreach (var line in paylines)
            {
                string sym = matrix[0, line[0]]; int match = 1;
                for (int c = 1; c < ColCount; c++) { if (matrix[c, line[c]] == sym) match++; else break; }
                if (match >= 3)
                {
                    totalWin += _multipliers[sym][match - 3] * lineBet;
                    for (int c = 0; c < match; c++) winCells.Add((c, line[c]));
                }
            }
            foreach (var cell in winCells) AnimateWinningCell(cell.Item1, cell.Item2);
            return totalWin;
        }

        private void AnimateWinningCell(int col, int row)
        {
            if (_drums[col].Children[FakeSymbolsCount + row] is Border b)
            {
                var sc = (b.RenderTransform as TransformGroup)?.Children[0] as ScaleTransform;
                if (sc != null)
                {
                    var a = new DoubleAnimation(1.0, 1.15, TimeSpan.FromMilliseconds(250))
                    { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
                    sc.BeginAnimation(ScaleTransform.ScaleXProperty, a);
                    sc.BeginAnimation(ScaleTransform.ScaleYProperty, a);
                }
                b.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 215, 0));
                b.BorderThickness = new Thickness(3);
            }
        }

        // ════════════════════════════════════════════════════════
        // RULETKA – budowanie stołu
        // ════════════════════════════════════════════════════════
        private void BuildRouletteTable()
        {
            var g = RouletteTableGrid;
            g.ColumnDefinitions.Clear();
            g.RowDefinitions.Clear();
            g.Children.Clear();
            _betButtons.Clear();
            _betLabels.Clear();
            _betOriginalBg.Clear();

            // Kolumny: zero(38) | 12×numer(40) | 2:1(45)
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            for (int i = 0; i < 12; i++)
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });

            // Wiersze: 3×numer(38) | tuzin(36) | zewnętrzne(42)
            for (int i = 0; i < 3; i++)
                g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });

            // ─── ZERO ───────────────────────────────────────────
            var b0 = MakeBetBtn("0", "0", Color.FromRgb(0, 120, 0));
            Grid.SetRow(b0, 0); Grid.SetColumn(b0, 0); Grid.SetRowSpan(b0, 3);
            g.Children.Add(b0);

            // ─── NUMERY 1–36 ────────────────────────────────────
            // Wiersz 0 → 3,6,9,...,36  (n%3==0)
            // Wiersz 1 → 2,5,8,...,35  (n%3==2)
            // Wiersz 2 → 1,4,7,...,34  (n%3==1)
            for (int col = 0; col < 12; col++)
            {
                int[] nums = { (col + 1) * 3, (col + 1) * 3 - 1, (col + 1) * 3 - 2 };
                for (int row = 0; row < 3; row++)
                {
                    int n = nums[row];
                    var btn = MakeBetBtn(n.ToString(), n.ToString(),
                        RedNums.Contains(n) ? Color.FromRgb(155, 10, 10) : Color.FromRgb(12, 12, 12));
                    Grid.SetRow(btn, row); Grid.SetColumn(btn, col + 1);
                    g.Children.Add(btn);
                }
            }

            // ─── ZAKŁADY KOLUMNOWE 2:1 ──────────────────────────
            // col3 → wiersz 0 (numery %3==0),  col2 → wiersz 1 (%3==2),  col1 → wiersz 2 (%3==1)
            (string key, int row)[] colBets = { ("col3", 0), ("col2", 1), ("col1", 2) };
            foreach (var (key, row) in colBets)
            {
                var btn = MakeBetBtn(key, "2:1", Color.FromRgb(8, 75, 8));
                Grid.SetRow(btn, row); Grid.SetColumn(btn, 13);
                g.Children.Add(btn);
            }

            // ─── TUZINY (wiersz 3) ──────────────────────────────
            (string key, string lbl)[] dozens =
            {
                ("dozen1","1–12"), ("dozen2","13–24"), ("dozen3","25–36")
            };
            for (int d = 0; d < 3; d++)
            {
                var btn = MakeBetBtn(dozens[d].key, dozens[d].lbl, Color.FromRgb(8, 70, 8));
                Grid.SetRow(btn, 3); Grid.SetColumn(btn, d * 4 + 1); Grid.SetColumnSpan(btn, 4);
                btn.Margin = new Thickness(d > 0 ? 2 : 0, 2, d < 2 ? 2 : 0, 2);
                g.Children.Add(btn);
            }

            // ─── ZAKŁADY ZEWNĘTRZNE (wiersz 4) ──────────────────
            // Każdy obejmuje 2 kolumny (6×2 = 12 kolumn numerów)
            (string key, string lbl, Color bg)[] outer =
            {
                ("low",   "1–18",        Color.FromRgb(8,70,8)),
                ("even",  "PARZYSTE",    Color.FromRgb(8,70,8)),
                ("red",   "🔴 CZERW.",   Color.FromRgb(155,10,10)),
                ("black", "⚫ CZARNE",   Color.FromRgb(12,12,12)),
                ("odd",   "NIEPARZ.",    Color.FromRgb(8,70,8)),
                ("high",  "19–36",       Color.FromRgb(8,70,8)),
            };
            for (int b = 0; b < 6; b++)
            {
                var btn = MakeBetBtn(outer[b].key, outer[b].lbl, outer[b].bg);
                Grid.SetRow(btn, 4); Grid.SetColumn(btn, b * 2 + 1); Grid.SetColumnSpan(btn, 2);
                btn.Margin = new Thickness(b > 0 ? 1 : 0, 2, b < 5 ? 1 : 0, 0);
                g.Children.Add(btn);
            }
        }

        private Button MakeBetBtn(string key, string label, Color bg)
        {
            var brush = new SolidColorBrush(bg);
            var btn = new Button
            {
                Tag = key,
                Background = brush,
                Style = (Style)FindResource("BetBtnStyle"),
            };
            SetBetContent(btn, label, 0);
            btn.Click += BetButton_Click;
            _betButtons[key] = btn;
            _betLabels[key] = label;
            _betOriginalBg[key] = new SolidColorBrush(bg);
            return btn;
        }

        // Ustawia zawartość przycisku (z kwotą zakładu lub bez)
        private void SetBetContent(Button btn, string label, int amount)
        {
            if (amount > 0)
            {
                var tb = new TextBlock { TextAlignment = TextAlignment.Center };
                tb.Inlines.Add(new Run(label) { FontSize = 10, FontWeight = FontWeights.Bold });
                tb.Inlines.Add(new LineBreak());
                tb.Inlines.Add(new Run($"[{amount}]") { FontSize = 9, Foreground = new SolidColorBrush(Colors.Gold) });
                btn.Content = tb;
            }
            else
            {
                btn.Content = new TextBlock
                {
                    Text = label,
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center
                };
            }
        }

        // ════════════════════════════════════════════════════════
        // RULETKA – wybór żetonu
        // ════════════════════════════════════════════════════════
        private void ChipButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string s && int.TryParse(s, out int val))
            {
                _selectedChip = val;
                HighlightChip(btn);
            }
        }

        private void HighlightChip(Button selected)
        {
            Button[] btns = { ChipBtn1, ChipBtn5, ChipBtn10, ChipBtn25, ChipBtn100, ChipBtn500 };
            for (int i = 0; i < btns.Length; i++)
            {
                bool isSelected = btns[i] == selected;
                btns[i].Background = new SolidColorBrush(
                    isSelected ? ChipColors[i].active : ChipColors[i].normal);
            }
        }

        // ════════════════════════════════════════════════════════
        // RULETKA – kliknięcie zakładu na stole
        // ════════════════════════════════════════════════════════
        private void BetButton_Click(object sender, RoutedEventArgs e)
        {
            if (_rouletteSpinning) return;
            if (sender is Button btn)
            {
                string key = (string)btn.Tag;
                if (!_rouletteBets.ContainsKey(key)) _rouletteBets[key] = 0;
                _rouletteBets[key] += _selectedChip;
                SetBetContent(btn, _betLabels[key], _rouletteBets[key]);
                RefreshTotalBetLabel();
            }
        }

        // ════════════════════════════════════════════════════════
        // RULETKA – wyczyść zakłady
        // ════════════════════════════════════════════════════════
        private void RouletteClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (_rouletteSpinning) return;
            _rouletteBets.Clear();
            foreach (var kvp in _betButtons)
            {
                SetBetContent(kvp.Value, _betLabels[kvp.Key], 0);
                if (_betOriginalBg.TryGetValue(kvp.Key, out var orig))
                    kvp.Value.Background = orig;
            }
            RouletteWheelStatus.Text = "Zakłady wyczyszczone. Postaw nowe!";
            RefreshTotalBetLabel();
        }

        // ════════════════════════════════════════════════════════
        // RULETKA – SPIN
        // ════════════════════════════════════════════════════════
        private async void RouletteSpinButton_Click(object sender, RoutedEventArgs e)
        {
            // Doładowanie gdy bez środków
            if (_money <= 0)
            {
                _money = 1000;
                RouletteSpinButton.Content = "KRĘĆ! 🎡";
                RefreshRouletteHeader("🎁 Saldo uzupełnione do 1000 monet!");
                return;
            }

            int totalBet = _rouletteBets.Values.Sum();
            if (totalBet == 0)
            {
                RouletteWheelStatus.Text = "⚠️ Najpierw postaw co najmniej jeden zakład!";
                return;
            }
            if (_money < totalBet)
            {
                RouletteWheelStatus.Text = $"⚠️ Niewystarczające środki! Masz {_money} monet, zakłady to {totalBet}.";
                return;
            }

            // Resetuj podświetlenia z poprzedniej rundy
            foreach (var kvp in _betOriginalBg)
                if (_betButtons.TryGetValue(kvp.Key, out var btn))
                    btn.Background = kvp.Value;

            _money -= totalBet;
            _rouletteSpinning = true;
            SetRouletteControls(false);

            // Losuj zwycięski numer
            int winNum = _random.Next(0, 37);

            // Animacja kołowrotu
            await AnimateWheel(winNum);

            // Oblicz wygraną
            int winnings = CalculateWin(winNum);
            _money += winnings;

            // Aktualizuj UI
            ApplyRouletteResult(winNum, winnings, totalBet);
            AddHistory(winNum);

            _rouletteSpinning = false;
            SetRouletteControls(true);

            // Tryb RESET gdy bez środków
            if (_money <= 0)
            {
                RouletteSpinButton.Content = "RESET 💸";
                RouletteSpinButton.IsEnabled = true;
            }
        }

        private async Task AnimateWheel(int finalNum)
        {
            RouletteWheelStatus.Text = "🎡 Koło się kręci…";
            RouletteColorName.Text = "";

            // Przyspieszenie → zwolnienie → zatrzymanie
            var delays = Enumerable.Repeat(55, 14)
                .Concat(Enumerable.Repeat(110, 8))
                .Concat(Enumerable.Repeat(190, 5))
                .ToArray();

            for (int i = 0; i < delays.Length; i++)
            {
                int display = (i < delays.Length - 1) ? _random.Next(0, 37) : finalNum;
                SetWheelDisplay(display, final: false);
                await Task.Delay(delays[i]);
            }
        }

        private void SetWheelDisplay(int num, bool final)
        {
            RouletteResultNumber.Text = num.ToString();

            Color bg;
            string name;
            if (num == 0)
            {
                bg = Color.FromRgb(0, 130, 0);
                name = "ZERO – zielone";
            }
            else if (RedNums.Contains(num))
            {
                bg = Color.FromRgb(180, 18, 18);
                name = "CZERWONE";
            }
            else
            {
                bg = Color.FromRgb(14, 14, 14);
                name = "CZARNE";
            }

            RouletteResultCircle.Background = new SolidColorBrush(bg);
            if (final)
                RouletteColorName.Text = $"{num}  ·  {name}";
        }

        private void ApplyRouletteResult(int winNum, int winnings, int totalBet)
        {
            SetWheelDisplay(winNum, final: true);
            HighlightWinners(winNum);

            int net = winnings - totalBet;
            if (winnings > 0)
            {
                RouletteWheelStatus.Text = $"🎉 Wygrana {winnings} monet! (netto: {net:+#;-#;0})";
            }
            else
            {
                RouletteWheelStatus.Text = $"😞 Brak wygranej tym razem.";
            }
            RouletteColorName.Text = $"Stawka: {totalBet}  ·  Zwrot: {winnings}  ·  Bilans: {net:+#;-#;0}";

            RouletteMoneyLabel.Text = $"💰  {_money:N0} monet";
            RouletteTotalBetLabel.Text = $"🎲  Stawka: {totalBet} monet";
        }

        // ════════════════════════════════════════════════════════
        // RULETKA – obliczanie wygranej
        // Standardowe wypłaty europejskiej ruletki:
        //   Prosto (1 numer)  → 35:1  (zwraca 36× stawkę)
        //   Split (2 numery)  → 17:1  (nie zaimpl.)
        //   Red/Black, Even/Odd, Low/High → 1:1  (zwraca 2×)
        //   Tuzin / Kolumna   → 2:1   (zwraca 3×)
        // ════════════════════════════════════════════════════════
        private int CalculateWin(int num)
        {
            int total = 0;
            foreach (var kvp in _rouletteBets)
            {
                if (kvp.Value <= 0) continue;
                int stake = kvp.Value, payout = 0;

                if (int.TryParse(kvp.Key, out int bn))
                {
                    // Zakład prosty – wygrana 35:1 (razem zwrot 36×)
                    if (bn == num) payout = stake * 36;
                }
                else switch (kvp.Key)
                    {
                        case "red": if (num > 0 && RedNums.Contains(num)) payout = stake * 2; break;
                        case "black": if (num > 0 && !RedNums.Contains(num)) payout = stake * 2; break;
                        case "even": if (num > 0 && num % 2 == 0) payout = stake * 2; break;
                        case "odd": if (num > 0 && num % 2 == 1) payout = stake * 2; break;
                        case "low": if (num >= 1 && num <= 18) payout = stake * 2; break;
                        case "high": if (num >= 19 && num <= 36) payout = stake * 2; break;
                        case "dozen1": if (num >= 1 && num <= 12) payout = stake * 3; break;
                        case "dozen2": if (num >= 13 && num <= 24) payout = stake * 3; break;
                        case "dozen3": if (num >= 25 && num <= 36) payout = stake * 3; break;
                        case "col1": if (num > 0 && num % 3 == 1) payout = stake * 3; break;
                        case "col2": if (num > 0 && num % 3 == 2) payout = stake * 3; break;
                        case "col3": if (num > 0 && num % 3 == 0) payout = stake * 3; break;
                    }
                total += payout;
            }
            return total;
        }

        // Podświetla przycisk jako wygrywający (tylko gdy gracz miał tam zakład)
        private void HighlightWinners(int winNum)
        {
            var winBg = new SolidColorBrush(Color.FromRgb(80, 65, 0));

            foreach (var kvp in _betButtons)
            {
                if (!_rouletteBets.TryGetValue(kvp.Key, out int bet) || bet <= 0) continue;

                bool won = false;
                if (int.TryParse(kvp.Key, out int bn)) won = (bn == winNum);
                else switch (kvp.Key)
                    {
                        case "red": won = winNum > 0 && RedNums.Contains(winNum); break;
                        case "black": won = winNum > 0 && !RedNums.Contains(winNum); break;
                        case "even": won = winNum > 0 && winNum % 2 == 0; break;
                        case "odd": won = winNum > 0 && winNum % 2 == 1; break;
                        case "low": won = winNum >= 1 && winNum <= 18; break;
                        case "high": won = winNum >= 19 && winNum <= 36; break;
                        case "dozen1": won = winNum >= 1 && winNum <= 12; break;
                        case "dozen2": won = winNum >= 13 && winNum <= 24; break;
                        case "dozen3": won = winNum >= 25 && winNum <= 36; break;
                        case "col1": won = winNum > 0 && winNum % 3 == 1; break;
                        case "col2": won = winNum > 0 && winNum % 3 == 2; break;
                        case "col3": won = winNum > 0 && winNum % 3 == 0; break;
                    }
                if (won) kvp.Value.Background = winBg;
            }
        }

        private void AddHistory(int num)
        {
            var col = num == 0 ? Color.FromRgb(0, 130, 0) :
                      RedNums.Contains(num) ? Color.FromRgb(180, 18, 18) :
                      Color.FromRgb(14, 14, 14);

            _rouletteHistory.Insert(0, new RouletteHistoryItem
            {
                Number = num.ToString(),
                Color = new SolidColorBrush(col)
            });
            if (_rouletteHistory.Count > 5)
                _rouletteHistory.RemoveAt(_rouletteHistory.Count - 1);
        }

        // ════════════════════════════════════════════════════════
        // RULETKA – pomocnicze metody UI
        // ════════════════════════════════════════════════════════
        private void RefreshRouletteHeader(string status = null)
        {
            RouletteMoneyLabel.Text = $"💰  {_money:N0} monet";
            RefreshTotalBetLabel();
            if (status != null) RouletteWheelStatus.Text = status;
        }

        private void RefreshTotalBetLabel()
        {
            int total = _rouletteBets.Values.Sum();
            RouletteTotalBetLabel.Text = $"🎲  Stawka: {total} monet";
            RouletteSpinButton.IsEnabled = total > 0 && _money >= total;
            RouletteClearButton.IsEnabled = total > 0;
        }

        private void SetRouletteControls(bool enabled)
        {
            RouletteSpinButton.IsEnabled = enabled;
            RouletteClearButton.IsEnabled = enabled;
            RouletteBackButton.IsEnabled = enabled;
            foreach (var btn in _betButtons.Values) btn.IsEnabled = enabled;
            Button[] chips = { ChipBtn1, ChipBtn5, ChipBtn10, ChipBtn25, ChipBtn100, ChipBtn500 };
            foreach (var c in chips) c.IsEnabled = enabled;
        }
    }
}