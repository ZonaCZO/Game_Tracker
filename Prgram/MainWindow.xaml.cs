using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32; 
using System.Diagnostics;
using System.ComponentModel;
using System.Windows.Data;

namespace Prgram
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<Game> myGames;
        private List<string> savedFolders = new List<string>(); // НОВЫЙ СПИСОК ПАПОК
        
        
        
        // Название файла, в который мы будем сохранять данные
        private const string FilePath = "my_games.json";

        public MainWindow()
        {
            InitializeComponent();
            LoadData(); // Пытаемся загрузить данные при старте
        }

        // --- НОВЫЙ МЕТОД: ЗАГРУЗКА ДАННЫХ ---
        private void LoadData()
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                try
                {
                    // Пытаемся загрузить новый формат (Игры + Папки)
                    var data = JsonSerializer.Deserialize<SaveDataModel>(json);
                    if (data != null)
                    {
                        myGames = data.Games ?? new ObservableCollection<Game>();
                        savedFolders = data.SavedFolders ?? new List<string>();
                    }
                }
                catch
                {
                    // Если произошла ошибка, значит файл старого формата (только список игр)
                    myGames = JsonSerializer.Deserialize<ObservableCollection<Game>>(json) ?? new ObservableCollection<Game>();
                    savedFolders = new List<string>();
                }
            }

            if (myGames == null) myGames = new ObservableCollection<Game>();

            GamesList.ItemsSource = myGames;

            // Сразу после загрузки запускаем сканирование сохраненных папок!
            RescanSavedFolders();
        }

        private void SaveData()
        {
            // Теперь мы упаковываем в JSON и игры, и папки
            var dataToSave = new SaveDataModel 
            { 
                Games = myGames, 
                SavedFolders = savedFolders 
            };
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(dataToSave, options);
            
            File.WriteAllText(FilePath, json);
        }

        // Переопределяем метод закрытия окна, чтобы сохранить данные перед выходом
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            SaveData();
            base.OnClosing(e);
        }
        

        // НОВЫЙ МЕТОД 1: Обновляет панель вкладок-тегов
        private void UpdateTagsTabs()
        {
            if (myGames == null) return;

            // Запоминаем текущую выбранную вкладку
            string currentSelection = TagTabs.SelectedItem as string ?? "Все игры";

            // Собираем все уникальные теги из всех игр
            var uniqueTags = myGames
                .SelectMany(g => g.Categories)
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            // Создаем список для UI
            var tabsList = new ObservableCollection<string> { "Все игры" };
            foreach (var tag in uniqueTags)
            {
                tabsList.Add(tag);
            }

            TagTabs.ItemsSource = tabsList;

            // Возвращаем выделение на ту же вкладку (если она еще существует)
            if (tabsList.Contains(currentSelection))
                TagTabs.SelectedItem = currentSelection;
            else
                TagTabs.SelectedItem = "Все игры";
        }

        // НОВЫЙ МЕТОД 2: Применяет фильтр к списку игр при клике на вкладку
        private void TagTabs_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            string selectedTag = TagTabs.SelectedItem as string;
            
            // Получаем "представление" нашего списка (чтобы фильтровать отображение, не удаляя сами данные)
            ICollectionView view = CollectionViewSource.GetDefaultView(myGames);

            if (selectedTag == null || selectedTag == "Все игры")
            {
                // Показываем всё
                view.Filter = null; 
            }
            else
            {
                // Показываем только те игры, у которых в списке категорий есть выбранный тег
                view.Filter = item =>
                {
                    if (item is Game game)
                    {
                        // Сравниваем без учета регистра (чтобы "Инди" и "инди" считались одним тегом)
                        return game.Categories.Any(c => c.Equals(selectedTag, System.StringComparison.OrdinalIgnoreCase));
                    }
                    return false;
                };
            }
        }
        
        // Вспомогательный метод: сканирует конкретную папку и возвращает количество новых найденных игр
        private int ScanFolderForNewGames(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return 0;

            string[] directories = Directory.GetDirectories(folderPath);
            int addedCount = 0;

            foreach (string dir in directories)
            {
                string gameName = new DirectoryInfo(dir).Name;
                string exePath = ScanForGameExecutable(dir);
                
                if (!string.IsNullOrEmpty(exePath))
                {
                    // Проверяем, нет ли уже такой игры
                    if (!myGames.Any(g => g.ExecutablePath == exePath || g.Title.ToLower() == gameName.ToLower()))
                    {
                        myGames.Add(new Game 
                        { 
                            Title = gameName, 
                            Mode = GameMode.NotCompleted, 
                            Categories = new List<string>(),
                            ExecutablePath = exePath
                        });
                        addedCount++;
                    }
                }
            }
            return addedCount;
        }

        // НОВЫЙ МЕТОД: Повторное сканирование при запуске
        private void RescanSavedFolders()
        {
            int totalAdded = 0;
            
            // Проходимся по всем папкам, которые мы когда-либо добавляли
            foreach (string folder in savedFolders)
            {
                totalAdded += ScanFolderForNewGames(folder);
            }
            
            // Если нашлись новые игры во время авто-сканирования, радуем пользователя
            if (totalAdded > 0)
            {
                MessageBox.Show($"С возвращением!\nАвтоматическое сканирование нашло новые игры: {totalAdded} шт.", 
                                "Обновление библиотеки", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
private GameMode GetSelectedMode(int selectedIndex)
{
    return selectedIndex switch
    {
        1 => GameMode.Completed,
        2 => GameMode.Sandbox,
        3 => GameMode.Online,
        _ => GameMode.NotCompleted
    };
}

// Обновленный метод добавления одной игры (с учетом режима)
private void AddGame_Click(object sender, RoutedEventArgs e)
{
    string title = GameTitleInput.Text;
    string categoriesText = GameCategoriesInput.Text;
    
    if (!string.IsNullOrWhiteSpace(title))
    {
        List<string> categoriesList = categoriesText
            .Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim())
            .ToList();

        myGames.Add(new Game { 
            Title = title, 
            Mode = GetSelectedMode(GameModeCombo.SelectedIndex), // Берем режим из выпадающего списка
            Categories = categoriesList,
            ExecutablePath = currentScannedExePath 
        });
        
        GameTitleInput.Clear();
        GameCategoriesInput.Clear(); 
        GameModeCombo.SelectedIndex = 0; // Сбрасываем режим на "Не пройдена"
        currentScannedExePath = null;
    }
    else
    {
        MessageBox.Show("Введите название игры!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}

// НОВЫЙ МЕТОД: Массовое сканирование папки (например D:\Games)
        private void ScanMultipleGames_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog folderDialog = new OpenFolderDialog
            {
                Title = "Выберите общую папку со всеми играми"
            };

            if (folderDialog.ShowDialog() == true)
            {
                string gamesFolder = folderDialog.FolderName;
                
                // СОХРАНЯЕМ ПАПКУ В БАЗУ, если ее там еще нет
                if (!savedFolders.Contains(gamesFolder))
                {
                    savedFolders.Add(gamesFolder);
                }

                // Запускаем наш универсальный сканер
                int addedCount = ScanFolderForNewGames(gamesFolder);

                MessageBox.Show($"Сканирование папки завершено!\nНовых игр добавлено: {addedCount}", 
                    "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

// НОВЫЙ МЕТОД: Запуск игры
    // 1. Событие: когда мы кликаем по списку (выделяем игры)
private void GamesList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
{
    int selectedCount = GamesList.SelectedItems.Count;

    // Кнопка ИГРАТЬ и поле переименования работают ТОЛЬКО если выделена ровно 1 игра
    PlayButton.IsEnabled = selectedCount == 1;
    RenameInput.IsEnabled = selectedCount == 1;

    // Если выделена одна игра, автоматически подставляем её имя в поле для удобства
    if (selectedCount == 1 && GamesList.SelectedItem is Game game)
    {
        RenameInput.Text = game.Title;
    }
    else
    {
        RenameInput.Clear();
    }
}

// 2. ОБНОВЛЕННЫЙ МЕТОД: Изменение статуса сразу у нескольких игр
private void ChangeMode_Click(object sender, RoutedEventArgs e)
{
    // Проверяем, есть ли хотя бы одна выделенная игра
    if (GamesList.SelectedItems.Count > 0)
    {
        GameMode newMode = GetSelectedMode(ChangeModeCombo.SelectedIndex);
        
        // GamesList.SelectedItems хранит объекты как object, поэтому нам нужно привести их к типу Game
        // Мы используем .Cast<Game>().ToList(), чтобы безопасно пройтись по всем выделенным играм
        foreach (Game selectedGame in GamesList.SelectedItems.Cast<Game>().ToList())
        {
            selectedGame.Mode = newMode;
        }
        
        // Обновляем список, чтобы новые статусы отобразились
        GamesList.ItemsSource = null;
        GamesList.ItemsSource = myGames;
    }
    else
    {
        MessageBox.Show("Сначала выделите хотя бы одну игру (используйте Ctrl или Shift для нескольких)!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

// 3. НОВЫЙ МЕТОД: Переименование игры
private void RenameGame_Click(object sender, RoutedEventArgs e)
{
    if (GamesList.SelectedItems.Count == 1 && GamesList.SelectedItem is Game selectedGame)
    {
        string newName = RenameInput.Text.Trim();
        if (!string.IsNullOrWhiteSpace(newName))
        {
            selectedGame.Title = newName;
            
            GamesList.ItemsSource = null;
            GamesList.ItemsSource = myGames;
        }
        else
        {
            MessageBox.Show("Имя игры не может быть пустым!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
    else
    {
        MessageBox.Show("Выберите ровно одну игру для переименования.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

// 4. ОБНОВЛЕННЫЙ МЕТОД: Запуск игры (добавлена проверка на количество выделенных игр)
private void PlayGame_Click(object sender, RoutedEventArgs e)
{
    // Двойная защита: проверяем, что выделена только одна игра
    if (GamesList.SelectedItems.Count == 1 && GamesList.SelectedItem is Game selectedGame)
    {
        if (!string.IsNullOrEmpty(selectedGame.ExecutablePath) && File.Exists(selectedGame.ExecutablePath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = selectedGame.ExecutablePath,
                WorkingDirectory = Path.GetDirectoryName(selectedGame.ExecutablePath),
                UseShellExecute = true
            });
        }
        else
        {
            MessageBox.Show("Путь к игре не найден! Возможно, игра удалена или перемещена.", "Ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

// НОВЫЙ МЕТОД: Удаление выделенных игр
private void DeleteGame_Click(object sender, RoutedEventArgs e)
{
    int selectedCount = GamesList.SelectedItems.Count;

    // Проверяем, выделено ли хоть что-то
    if (selectedCount > 0)
    {
        // Спрашиваем пользователя, точно ли он хочет удалить игры
        MessageBoxResult result = MessageBox.Show(
            $"Вы уверены, что хотите убрать {selectedCount} игр(у) из вашего списка?\n\n(Сами файлы игр на компьютере удалены не будут)", 
            "Подтверждение удаления", 
            MessageBoxButton.YesNo, 
            MessageBoxImage.Question);

        // Если пользователь нажал "Да"
        if (result == MessageBoxResult.Yes)
        {
            // СОЗДАЕМ КОПИЮ списка выделенных игр. 
            // Это важно! Если удалять элементы напрямую из GamesList.SelectedItems, 
            // программа выдаст ошибку, так как список выделения начнет меняться прямо во время цикла.
            var gamesToDelete = GamesList.SelectedItems.Cast<Game>().ToList();

            // Удаляем каждую игру из нашей главной коллекции myGames
            foreach (var game in gamesToDelete)
            {
                myGames.Remove(game);
            }

            // Обновляем отображение списка
            GamesList.ItemsSource = null;
            GamesList.ItemsSource = myGames;
            
            // Очищаем поля переименования на всякий случай
            RenameInput.Clear();
        }
    }
    else
    {
        MessageBox.Show("Сначала выделите игры, которые хотите удалить (используйте Ctrl или Shift для выбора нескольких).", 
            "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}        
        private readonly string[] _excludedExes = { "unitycrashhandler64.exe", "unitycrashhandler32.exe", "unins000.exe", "uninstall.exe", "crashreporter.exe" };
        private readonly string[] _priorityFolders = { "bin", "game", "binaries", "win64", "release" };

        private string ScanForGameExecutable(string rootPath)
        {
            // 1. Ищем в корневой папке
            var rootExes = GetValidExes(rootPath);
            if (rootExes.Count == 1) return rootExes[0];
            if (rootExes.Count > 1) return AskUserToChoose(rootExes);

            // 2. Ищем в приоритетных подпапках (bin, game и т.д.)
            if (Directory.Exists(rootPath))
            {
                var dirs = Directory.GetDirectories(rootPath);
                foreach (var dir in dirs)
                {
                    string dirName = new DirectoryInfo(dir).Name.ToLower();
                    if (_priorityFolders.Contains(dirName))
                    {
                        var exes = GetValidExes(dir);
                        if (exes.Count == 1) return exes[0];
                        if (exes.Count > 1) return AskUserToChoose(exes);
                    }
                }

                // 3. Если ничего не нашли, ищем во всех остальных подпапках
                var allExes = Directory.GetFiles(rootPath, "*.exe", SearchOption.AllDirectories)
                    .Where(exe => !IsExcluded(exe))
                    .ToList();

                if (allExes.Count == 1) return allExes[0];
                if (allExes.Count > 1) return AskUserToChoose(allExes);
            }

            return null; // Если вообще ничего не нашли
        }
        

// Вспомогательный метод: получает правильные .exe в указанной папке
        private List<string> GetValidExes(string path)
        {
            if (!Directory.Exists(path)) return new List<string>();
            return Directory.GetFiles(path, "*.exe")
                .Where(exe => !IsExcluded(exe))
                .ToList();
        }

// Вспомогательный метод: проверяет, не в черном ли списке файл
        private bool IsExcluded(string exePath)
        {
            string fileName = Path.GetFileName(exePath).ToLower();
            return _excludedExes.Contains(fileName);
        }
        
        
        private string AskUserToChoose(List<string> options)
        {
            // Создаем новое окно программно
            Window dialog = new Window
            {
                Title = "Найдено несколько файлов",
                Width = 450,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, // Делает окно зависимым от главного
                ResizeMode = ResizeMode.NoResize
            };

            StackPanel panel = new StackPanel { Margin = new Thickness(10) };
    
            panel.Children.Add(new TextBlock { 
                Text = "В папке найдено несколько .exe файлов.\nПожалуйста, выберите тот, который запускает игру:", 
                Margin = new Thickness(0, 0, 0, 10) 
            });

            ListBox listBox = new ListBox { 
                ItemsSource = options, 
                Height = 150, 
                Margin = new Thickness(0, 0, 0, 10) 
            };
            panel.Children.Add(listBox);

            Button btnSelect = new Button { Content = "Выбрать этот файл", Height = 30 };
            string selectedExe = null;

            btnSelect.Click += (s, e) => {
                if (listBox.SelectedItem != null)
                {
                    selectedExe = listBox.SelectedItem.ToString();
                    dialog.DialogResult = true; // Закрывает окно
                }
                else
                {
                    MessageBox.Show("Сначала выделите файл в списке!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            panel.Children.Add(btnSelect);

            dialog.Content = panel;
            dialog.ShowDialog(); // Показывает окно и ждет, пока пользователь его закроет

            return selectedExe;
        }
        private string currentScannedExePath = null; // Временная переменная для хранения пути

        private void ScanFolder_Click(object sender, RoutedEventArgs e)
        {
            // Открываем диалог выбора папки (Доступно в .NET 8 WPF)
            OpenFolderDialog folderDialog = new OpenFolderDialog
            {
                Title = "Выберите папку с игрой"
            };

            if (folderDialog.ShowDialog() == true)
            {
                string selectedFolder = folderDialog.FolderName;
                string foundExe = ScanForGameExecutable(selectedFolder);

                if (foundExe != null)
                {
                    currentScannedExePath = foundExe;
            
                    // Если поле названия пустое, берем имя папки как название игры
                    if (string.IsNullOrWhiteSpace(GameTitleInput.Text))
                    {
                        GameTitleInput.Text = new DirectoryInfo(selectedFolder).Name;
                    }
            
                    MessageBox.Show($"Файл найден и привязан:\n{Path.GetFileName(foundExe)}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("В этой папке не найдено подходящих .exe файлов.", "Не найдено", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        
    }
    
    
}