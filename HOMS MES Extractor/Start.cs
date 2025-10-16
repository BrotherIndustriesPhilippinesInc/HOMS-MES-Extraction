using Newtonsoft.Json;
using NPOI.OpenXmlFormats.Vml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HOMS_MES_Extractor
{
    public partial class Start : Form
    {
        private System.Windows.Forms.Timer _timer;
        private readonly Random _random = new Random();

        public Start()
        {
            InitializeComponent();

            StartRandomCatLoop();
        }

        private async Task StartHourlyExtractionAt50()
        {
            while (true)
            {
                DateTime now = DateTime.Now;
                Debug.WriteLine($"Time: {DateTime.Now}");

                // Calculate next occurrence at :50
                DateTime nextRun = new DateTime(now.Year, now.Month, now.Day, now.Hour, 50, 0);
                if (now.Minute >= 50) // if past :50 this hour, schedule next hour
                {
                    nextRun = nextRun.AddHours(1);
                }

                TimeSpan waitTime = nextRun - now;
                Console.WriteLine($"Next extraction scheduled at {nextRun} (in {waitTime.TotalMinutes:F1} minutes)");

                await Task.Delay(waitTime); // wait until :50

                try
                {
                    Main main = new Main();
                    main.Show();

                    Console.WriteLine($"{DateTime.Now}: Extraction + bulk post completed.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during extraction: {ex.Message}");
                }
            }
        }

        private async void Start_Load(object sender, EventArgs e)
        {
            await LoadRandomCatAsync();
            await StartHourlyExtractionAt50();
        }

        public class CatImage
        {
            public string url { get; set; }
        }

        private async Task LoadRandomCatAsync()
        {
            try
            {
                using HttpClient client = new HttpClient();
                string json = await client.GetStringAsync("https://api.thecatapi.com/v1/images/search");

                // Parse JSON
                var cats = JsonConvert.DeserializeObject<List<CatImage>>(json);
                if (cats != null && cats.Count > 0)
                {
                    string imageUrl = cats[0].url;

                    // Load image into PictureBox asynchronously
                    pictureBox1.LoadAsync(imageUrl);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load cat image: " + ex.Message);
            }
        }

        private async void StartRandomCatLoop()
        {
            while (true)
            {
                await LoadRandomCatAsync();

                // Random delay between 5s (5000ms) and 60s (60000ms)
                int delay = _random.Next(5000, 60001);
                await Task.Delay(delay);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Main main = new Main();
            main.Show();
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            await LoadRandomCatAsync();
        }
    }
}
