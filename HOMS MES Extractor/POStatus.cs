using IQCSystemV2.Functions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HOMS_MES_Extractor
{
    public partial class POStatus: Form
    {
        private WebViewFunctions webViewFunctions;
        private string username = "ZZPDE31G";
        private string password = "ZZPDE31G";
        private bool isQueryClicked = false;
        private bool didDownload = false;

        private string targetDir = @"\\apbiphsh07\D0_ShareBrotherGroup\19_BPS\17_Installer\HOMSV2\PR1_PO_Status\";

        public POStatus()
        {
            // === 1. THE "KILL SWITCH" (Your Request) ===
            // Check for any OTHER open instances of POStatus and close them.
            // We use ToList() to make a copy so we can modify the collection while iterating.
            var existingForms = Application.OpenForms.OfType<POStatus>()
                                       .Where(f => f != this) // Don't close the one we are creating right now!
                                       .ToList();

            foreach (var form in existingForms)
            {
                form.Close();
                form.Dispose();
            }
            // ==========================================

            InitializeComponent();

            webViewFunctions = new WebViewFunctions(webView21);

            Uri emes_link = new Uri("http://" + username + ":" + password + "@10.248.1.10/BIPHMES/FLoginNew.aspx");
            webView21.Source = emes_link;
        }

        private async Task Login(string username, string password, Uri link)
        {
            try
            {
                await webViewFunctions.SetTextBoxValueAsync("id", "txtUserCode", username);
                await Task.Delay(100);
                await webViewFunctions.SetTextBoxValueAsync("id", "txtPassword", password);
                await Task.Delay(100);
                await webViewFunctions.ClickButtonAsync("id", "cmdSubmit");

                webView21.Source = link;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private async void webView21_CoreWebView2InitializationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2InitializationCompletedEventArgs e)
        {
            //LOGIN
            Uri ordersCheckTable = new Uri("http://" + username + ":" + password + "@10.248.1.10/BIPHMES/MOMODEL/FMOMP.ASPX");
            await Login(username, password, ordersCheckTable);

            webViewFunctions.AddDownloadStartingHandler(async (sender, args) =>
            {
                //MessageBox.Show("File Downloaded");
                args.Handled = false;

                var download = args.DownloadOperation;

                download.StateChanged += async (s, e) =>
                {
                    if (download.State == Microsoft.Web.WebView2.Core.CoreWebView2DownloadState.Completed)
                    {
                        try
                        {
                            string currentPath = download.ResultFilePath; // final saved location
                            string fileName = System.IO.Path.GetFileName(currentPath);

                            string targetPath = System.IO.Path.Combine(targetDir, fileName);

                            // Just in case the folder doesn’t exist
                            System.IO.Directory.CreateDirectory(targetDir);

                            // 💣 Delete all existing files in the folder first
                            foreach (var file in System.IO.Directory.GetFiles(targetDir))
                            {
                                try
                                {
                                    System.IO.File.Delete(file);
                                }
                                catch (Exception delEx)
                                {
                                    Console.WriteLine($"Failed to delete {file}: {delEx.Message}");
                                }
                            }

                            // If file already exists there, delete or rename it
                            if (System.IO.File.Exists(targetPath))
                            {
                                System.IO.File.Delete(targetPath);
                            }

                            // Move the file
                            System.IO.File.Move(currentPath, targetPath);

                            // Now run your post-download logic
                            List<PoRecord> poRecords = await ExtractData();
                            await PostData(poRecords);


                            //MessageBox.Show($"File moved successfully to:\n{targetPath}", "Done");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Error moving file: " + ex.Message, "Error");
                        }
                    }
                    else if (download.State == Microsoft.Web.WebView2.Core.CoreWebView2DownloadState.Interrupted)
                    {
                        MessageBox.Show("Download interrupted: " + download.InterruptReason);
                    }
                };
            });
        }

        private async Task ShowData()
        {
            await webViewFunctions.SetTextBoxValueAsync("name", "txtItemCodeQuery", "8CHA");
            await webViewFunctions.ClickButtonAsync("id", "chbImportDate");

            //Insert the current date
            await webViewFunctions.SetTextBoxValueAsync("name", "ImportDateTo$GuruDate", DateTime.Now.ToString("yyyy-MM-dd"));
            //Insert the past 30 days of the current date
            await webViewFunctions.SetTextBoxValueAsync("name", "ImportDateFrom$GuruDate", DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd"));

            await webViewFunctions.ClickButtonAsync("id", "cmdQuery");

            isQueryClicked = true;
        }

        private async Task ProdDataDisplay()
        {
            string isLoaded = await webViewFunctions.GetElementText("id", "lblTitle");
            isLoaded = isLoaded.Trim('"');
            if (isLoaded == "PO Management" && !isQueryClicked)
            {
                int retries = 0;
                while (retries < 50)
                {
                    if (!string.IsNullOrWhiteSpace(isLoaded) && isLoaded != "null")
                        break;

                    retries++;
                    await Task.Delay(3000);
                }

                bool isTableLoaded = await webViewFunctions.HasChildrenAsync("id", "gridWebGrid");
                if (!isTableLoaded && !isQueryClicked)
                {
                    await Task.Delay(3000);
                    await ShowData();
                }
            }
        }

        private async Task<bool> CheckGraph()
        {
            bool tableLoaded = false;

            bool isTableLoaded = await webViewFunctions.HasChildrenAsync("id", "gridWebGridDiv");
            if (isTableLoaded)
            {
                tableLoaded = true;
            }

            return tableLoaded;
        }

        private async Task DownloadFile()
        {
            await webViewFunctions.ExecuteJavascript($"document.getElementById(\"cmdGridExport\").removeAttribute(\"disabled\");\r\n");
            await webViewFunctions.ClickButtonAsync("id", "cmdGridExport");
            didDownload = true;
        }

        public class PoRecord
        {
            public string PO { get; set; }
            public string POType { get; set; }
            public string ModelCode { get; set; }
            public int PlannedQty { get; set; }
            public int ProducedQty { get; set; }
            public int FinishedQty { get; set; }
            public string Production { get; set; }
            public string ProdLine { get; set; }

            public string ActualStart { get; set; }

            public DateTime StartDateTime { get; set; }
        }

        private async Task<List<PoRecord>> ExtractData()
        {
            string folderPath = @"\\apbiphsh07\D0_ShareBrotherGroup\19_BPS\17_Installer\HOMSV2\PR1_PO_Status\";

            // 🧩 Get the only file in the folder
            var file = Directory.GetFiles(folderPath).FirstOrDefault();
            if (file == null)
            {
                MessageBox.Show("No downloaded file found!", "Error");
                return new List<PoRecord>();
            }

            try
            {
                // Get original name and new path
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                string newPath = Path.Combine(folderPath, fileNameWithoutExt + ".csv");

                // If .csv already exists, delete it first
                if (File.Exists(newPath))
                    File.Delete(newPath);

                // Rename file to .csv
                File.Move(file, newPath);

                // 🧩 Read and parse the CSV
                var records = new List<PoRecord>();
                var lines = File.ReadAllLines(newPath)
                                .Where(l => !string.IsNullOrWhiteSpace(l))
                                .ToList();

                if (lines.Count < 2)
                {
                    //MessageBox.Show("No data rows found in CSV!", "Error");
                    Console.WriteLine("No data rows found in CSV!");
                    return new List<PoRecord>();
                }

                // Detect separator automatically (comma or tab)
                char separator = lines[0].Contains('\t') ? '\t' : ',';

                // Parse header to find column count
                var headers = lines[0].Split(separator);

                int poIndex = 1;
                int poTypeIndex = 3;
                int modelCodeIndex = 4;
                int plannedQtyIndex = 5;
                int producedQtyIndex = 6;
                int finishedQtyIndex = 7;
                int productionIndex = 11;
                int prodLineIndex = 17;
                int actualStartIndex = 14;

                foreach (var line in lines.Skip(1))
                {
                    var cols = line.Split(separator);

                    string po = cols[poIndex].Trim();
                    string poType = cols[poTypeIndex].Trim();
                    string modelCode = cols[modelCodeIndex].Trim();
                    int plannedQty = int.Parse(cols[plannedQtyIndex]);
                    int producedQty = int.Parse(cols[producedQtyIndex]);
                    int finishedQty = int.Parse(cols[finishedQtyIndex]);
                    string production = cols[productionIndex];
                    string prodLine = cols[prodLineIndex].Trim();
                    string actualStart = cols[actualStartIndex].Trim();

                    records.Add(new PoRecord
                    {
                        PO = po,
                        POType = poType,
                        ModelCode = modelCode,
                        PlannedQty = plannedQty,
                        ProducedQty = producedQty,
                        FinishedQty = finishedQty,
                        Production = production,
                        ProdLine = prodLine,
                        ActualStart = actualStart,

                        StartDateTime = DateTime.UtcNow
                    });
                    
                }
                return records;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing file: {ex.Message}", "Error");
                return new List<PoRecord>(); // ✅ Ensure this always returns
            }
        }

        private async Task PostData(List<PoRecord> extractedRecords)
        {
            if (extractedRecords == null || !extractedRecords.Any())
                return;

            // Map extracted PoRecord -> API PoRecord
            DateTime now = DateTime.UtcNow;
            var apiRecords = extractedRecords.Select(r => new Core.POStatus
            {
                PO = r.PO,
                POType = r.POType,
                ModelCode = r.ModelCode,
                PlannedQty = r.PlannedQty,
                ProducedQty = r.ProducedQty,
                FinishedQty = r.FinishedQty,
                Production = r.Production,
                ProdLine = r.ProdLine,
                ActualStart = r.ActualStart,
                StartDateTime = now
            }).ToList();

            using var client = new HttpClient();
            client.BaseAddress = new Uri("http://apbiphbpswb01:9876/");
            //client.BaseAddress = new Uri("https://localhost:7046/");

            try
            {
                string json = JsonConvert.SerializeObject(apiRecords);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync("api/POStatus/CheckActivityEventStream", content);

                if (response.IsSuccessStatusCode)
                {
                    string respContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("insert successful: " + respContent);
                    this.Close();
                }
                else
                {
                    string respContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Failed: {response.StatusCode}, {respContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error posting data: " + ex.Message);
            }
        }

        private async void webView21_NavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            try
            {
                await ProdDataDisplay();

                if (await CheckGraph() && !didDownload)
                {
                    await DownloadFile();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

    }
}
