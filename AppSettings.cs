using System;
using System.IO;
using System.Text.Json;

namespace CameraApp
{
    public class AppSettings
    {
        private static readonly string SettingsFileName = "settings.json";
        private static readonly string SettingsFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            SettingsFileName);

        public string OutputDirectory { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
            "CameraApp");
        
        public decimal CaptureDelay { get; set; } = 0;
        
        public decimal RecordDuration { get; set; } = 10;
        
        public int BurstCount { get; set; } = 1; // 連拍數量（一秒內拍攝的張數）

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        // 驗證目錄是否存在，如果不存在則使用預設值
                        if (string.IsNullOrWhiteSpace(settings.OutputDirectory) || 
                            !Directory.Exists(settings.OutputDirectory))
                        {
                            settings.OutputDirectory = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                                "CameraApp");
                        }
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                // 如果讀取失敗，記錄錯誤並返回預設設定
                System.Diagnostics.Debug.WriteLine($"載入設定檔失敗：{ex.Message}");
            }

            // 如果檔案不存在或讀取失敗，創建預設設定並儲存
            var defaultSettings = new AppSettings();
            defaultSettings.Save();
            return defaultSettings;
        }

        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"儲存設定檔失敗：{ex.Message}");
            }
        }
    }
}
