using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Prgram;

namespace Prgram
{
    // Наши новые режимы
    public enum GameMode
    {
        NotCompleted,
        Completed,
        Sandbox,
        Online
    }

    public class Game
    {
        public string Title { get; set; }
        
        // Теперь вместо IsCompleted используем Mode
        public GameMode Mode { get; set; } = GameMode.NotCompleted; 
        
        public List<string> Categories { get; set; } = new List<string>();
        public string ExecutablePath { get; set; }

        [JsonIgnore]
        public string DisplayText
        {
            get
            {
                string categoriesString = Categories.Count > 0 ? $" [{string.Join(", ", Categories)}]" : "";
                
                // Выбираем значок и текст в зависимости от режима
                string modeText = Mode switch
                {
                    GameMode.Completed => "✅ [Пройдена]",
                    GameMode.Sandbox => "🏖️ [Песочница]",
                    GameMode.Online => "🌐 [Онлайн]",
                    _ => "❌ [Не пройдена]" // NotCompleted
                };

                return $"{modeText} {Title}{categoriesString}";
            }
        }
    }
}
// Класс-контейнер для сохранения и игр, и папок
public class SaveDataModel
{
    public ObservableCollection<Game> Games { get; set; } = new ObservableCollection<Game>();
    public List<string> SavedFolders { get; set; } = new List<string>();
}