using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IQCSystemV2.Functions
{
    internal class WebViewFunctions
    {
        private WebView2 _webView;
        private TaskCompletionSource<bool> navigationCompletionSource;
        public event EventHandler<CoreWebView2NavigationStartingEventArgs> NavigationStarting;
        public event EventHandler<CoreWebView2NavigationCompletedEventArgs> NavigationCompleted;
        private readonly List<EventHandler<CoreWebView2NavigationStartingEventArgs>> _navigationStartingHandlers = new List<EventHandler<CoreWebView2NavigationStartingEventArgs>>();

        private event EventHandler<CoreWebView2DownloadStartingEventArgs> DownloadStarting;

        public event EventHandler<CoreWebView2ScriptDialogOpeningEventArgs> ScriptDialogOpening;
        public WebViewFunctions(WebView2 _webView, bool CustomEnvironment = false)
        {
            this._webView = _webView;

            if (CustomEnvironment)
            {
                // Explicitly await the initialization
                _ = InitializeAsync(true); // Fire-and-forget for now, or handle it with an async factory pattern
            }
        }

        public async Task InitializeAsync()
        {
            try
            {
                await _webView.EnsureCoreWebView2Async(null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during WebView2 initialization: {ex.Message}");
                MessageBox.Show($"Error during WebView2 initialization: {ex.Message}");
            }
        }

        public async Task InitializeAsync(bool CustomEnvironment = false)
        {
            try
            {
                if (CustomEnvironment)
                {
                    // Define the custom cookies directory
                    string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    string cookiesPath = Path.Combine(documentsPath, "WebView2Cookies");

                    // Ensure the directory exists
                    if (!Directory.Exists(cookiesPath))
                    {
                        Directory.CreateDirectory(cookiesPath);
                        Console.WriteLine("Directory created.");
                    }
                    else
                    {
                        Console.WriteLine("Directory already exists. Proceeding...");
                    }

                    // Create the WebView2 environment
                    var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: cookiesPath);

                    // Initialize WebView2 with the environment
                    await _webView.EnsureCoreWebView2Async(environment);
                    Console.WriteLine($"WebView2 initialized with User Data Folder: {cookiesPath}");
                }
                else
                {
                    await _webView.EnsureCoreWebView2Async(null);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during WebView2 initialization: {ex.Message}");
                MessageBox.Show($"Error during WebView2 initialization: {ex.Message}");
            }
        }

        public async Task InitializeInMemoryAsync()
        {
            try
            {
                // Create an in-memory user data folder
                var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: null);

                await _webView.EnsureCoreWebView2Async(environment);

                Console.WriteLine("WebView2 initialized with an in-memory user data folder.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during WebView2 initialization: {ex.Message}");
            }
        }

        public async Task LoadPageAsync(string url, string element = "", int timeout = 5000, bool AreDefaultScriptDialogsEnabled = true)
        {
            await _webView.EnsureCoreWebView2Async(null);
            //DISABLE ALL ALERTS
            _webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = AreDefaultScriptDialogsEnabled;

            navigationCompletionSource = new TaskCompletionSource<bool>();
            _webView.CoreWebView2.Navigate(url);

            bool isLoaded = false;
            if (element != "")
            {
                isLoaded = await WaitForElementToExistAsync(element, timeout);
            }


            if (isLoaded == false)
            {
                Debug.WriteLine(url + " Not Loaded");
            }
            await Task.Delay(100);
        }

        public async Task SetTextBoxValueAsync(string searchBy, string name, string inputText, int classIndex = 0, int timeoutMilliseconds = 5000)
        {
            searchBy = searchBy.ToLower();
            string script;
            int elapsedTime = 0;
            const int delayInterval = 100;

            while (elapsedTime < timeoutMilliseconds)
            {
                switch (searchBy)
                {
                    case "id":
                        script = $"document.getElementById('{name}').value = `{inputText}`;";
                        await _webView.CoreWebView2.ExecuteScriptAsync(script);
                        await Task.Delay(100);
                        break;

                    case "class":
                        script = $"document.getElementsByClassName('{name}')[{classIndex}].value = `{inputText}`;";
                        await _webView.CoreWebView2.ExecuteScriptAsync(script);
                        await Task.Delay(100);
                        break;

                    case "name":
                        script = $"document.querySelector(\"[name='{name}']\").value = `{inputText}`;";
                        await _webView.CoreWebView2.ExecuteScriptAsync(script);
                        await Task.Delay(100);
                        break;

                    default:
                        throw new ArgumentException($"Invalid searchBy parameter: {searchBy}");
                }

                // Check if the value has been set successfully
                if (await WaitForContentToExistAsync(searchBy, name, inputText))
                {
                    return; // Exit the method if the value is set
                }

                // Wait and increment elapsed time
                await Task.Delay(delayInterval);
                elapsedTime += delayInterval;
            }

            // Throw a timeout exception if the value couldn't be set within the time limit
            throw new TimeoutException($"Setting the value of the element '{name}' by '{searchBy}' timed out after {timeoutMilliseconds} milliseconds.");
        }

        public async Task SetTextAreaValueAsync(string searchBy, string name, string inputText, int classIndex = 0, int timeoutMilliseconds = 5000)
        {
            searchBy = searchBy.ToLower();
            string script;
            int elapsedTime = 0;
            const int delayInterval = 100;

            while (elapsedTime < timeoutMilliseconds)
            {
                switch (searchBy)
                {
                    case "id":
                        script = $"document.getElementById('{name}').value = `{inputText}`;";
                        await _webView.CoreWebView2.ExecuteScriptAsync(script);
                        await Task.Delay(100);
                        break;

                    case "class":
                        script = $"document.getElementsByClassName('{name}')[{classIndex}].value = `{inputText}`;";
                        await _webView.CoreWebView2.ExecuteScriptAsync(script);
                        await Task.Delay(100);
                        break;

                    case "name":
                        script = $"document.querySelector(\"[name='{name}']\").value = `{inputText}`;";
                        await _webView.CoreWebView2.ExecuteScriptAsync(script);
                        await Task.Delay(100);
                        break;
                }
                // Check if the value has been set successfully
                if (await WaitForContentToExistAsync(searchBy, name, inputText))
                {
                    return; // Exit the method if the value is set
                }

                // Wait and increment elapsed time
                await Task.Delay(delayInterval);
                elapsedTime += delayInterval;
            }
            throw new TimeoutException($"Setting the value of the element '{name}' by '{searchBy}' timed out after {timeoutMilliseconds} milliseconds.");
        }

        public async Task<string> GetTextBoxValue(string searchBy, string name)
        {
            searchBy = searchBy.ToLower();
            string script;
            string result;

            switch (searchBy)
            {
                case "id":
                    script = $"document.getElementById(\"{name}\").value;";
                    result = await _webView.CoreWebView2.ExecuteScriptAsync(script);
                    await Task.Delay(100);
                    result = result.Trim('"');
                    result = System.Text.RegularExpressions.Regex.Unescape(result);
                    return result;

                case "class":
                    script = $"document.getElementsByClassName(\"{name}\").value";
                    result = await _webView.CoreWebView2.ExecuteScriptAsync(script);
                    await Task.Delay(100);
                    result = result.Trim('"');
                    result = System.Text.RegularExpressions.Regex.Unescape(result);
                    return result;

                case "name":
                    script = $"document.querySelector(\"[name='{name}']\").value;";
                    result = await _webView.CoreWebView2.ExecuteScriptAsync(script);
                    await Task.Delay(100);
                    result = result.Trim('"');
                    result = System.Text.RegularExpressions.Regex.Unescape(result);
                    return result;
            }
            return "Get TextBox Value ERROR";
        }

        public async Task SelectElement(string searchBy, string name, string valueName, int classIndex = 0)
        {
            searchBy = searchBy.ToLower();
            string script;
            switch (searchBy)
            {
                case "id":
                    script = $"document.getElementById('{name}').value = '{valueName}';";
                    await _webView.CoreWebView2.ExecuteScriptAsync(script);
                    await Task.Delay(100);
                    break;

                case "class":
                    script = $"document.getElementsByClassName('{name}')[{classIndex}].value = '{valueName}';";
                    await _webView.CoreWebView2.ExecuteScriptAsync(script);
                    await Task.Delay(100);
                    break;

                case "name":
                    script = $"document.querySelector(\"[name='{name}']\").value = '{valueName}';";

                    await _webView.CoreWebView2.ExecuteScriptAsync(script);
                    await Task.Delay(100);
                    break;
            }
        }

        public async Task ClickButtonAsync(string searchBy, string name, int index = 0)
        {
            searchBy = searchBy.ToLower();
            string script;

            switch (searchBy)
            {
                case "id":
                    script = $"document.getElementById('{name}').click();";
                    await _webView.CoreWebView2.ExecuteScriptAsync(script);
                    await Task.Delay(100);
                    break;

                case "class":
                    script = $"document.getElementsByClassName('{name}')[{index}].click();";
                    await _webView.CoreWebView2.ExecuteScriptAsync(script);
                    await Task.Delay(100);
                    break;

                case "name":
                    script = $"document.querySelector(\"[name='{name}']\").click();";

                    await _webView.CoreWebView2.ExecuteScriptAsync(script);
                    await Task.Delay(100);
                    break;

                case "content":
                    script = $@"
                    var elements = document.querySelectorAll('*');
                    for (var i = 0; i < elements.length; i++) {{
                        if (elements[i].textContent.trim() === '{name}') {{
                            elements[i].click();
                            break;
                        }}
                    }}";
                    await _webView.CoreWebView2.ExecuteScriptAsync(script);
                    await Task.Delay(100);
                    break;
            }
        }

        public void AddNavigationStartingHandler(EventHandler<CoreWebView2NavigationStartingEventArgs> handler)
        {
            // Ensure we're adding to CoreWebView2's NavigationStarting event
            _webView.CoreWebView2.NavigationStarting += handler;
            NavigationStarting += handler;
            Debug.WriteLine("Starting handler added");
        }

        public void RemoveNavigationStartingHandler(EventHandler<CoreWebView2NavigationStartingEventArgs> handler)
        {
            if (_webView?.CoreWebView2 != null)
            {
                _webView.CoreWebView2.NavigationStarting -= handler;
                NavigationStarting -= handler;
                Debug.WriteLine("Starting handler removed");
            }
        }

        public void AddNavigationCompletedHandler(EventHandler<CoreWebView2NavigationCompletedEventArgs> handler)
        {
            _webView.CoreWebView2.NavigationCompleted += handler;
            _webView.NavigationCompleted += handler;
            Debug.WriteLine("Completed handler added");
        }

        public void RemoveNavigationCompletedHandler(EventHandler<CoreWebView2NavigationCompletedEventArgs> handler)
        {
            if (_webView?.CoreWebView2 != null)
            {
                _webView.CoreWebView2.NavigationCompleted -= handler;
                NavigationCompleted -= handler;
                Debug.WriteLine("Completed handler removed");
            }
        }

        public bool HasNavigationStartingHandlers()
        {
            return _navigationStartingHandlers.Count > 0; // Check if there are any attached handlers
        }

        // Example method to raise the event
        protected virtual void OnNavigationStarting(CoreWebView2NavigationStartingEventArgs e)
        {
            NavigationStarting?.Invoke(this, e);
        }

        public async Task<bool> WaitForElementToExistAsync(string cssSelector, Action callback, int timeoutMilliseconds = 5000, int maxRetries = 50)
        {
            try
            {
                var startTime = DateTime.Now;
                var retryCount = 0;
                var scriptCheckElement = $@"
                    (function waitElement() {{
                        var element = document.querySelector(""{cssSelector}"");
                        return element !== null;
                    }})();
                ";

                while (retryCount < maxRetries)
                {


                    // Execute the JavaScript to check for the element
                    var result = await _webView.ExecuteScriptAsync(scriptCheckElement);
                    await Task.Delay(100);

                    // If the element is found
                    if (result == "true")
                    {
                        callback?.Invoke(); // Invoke the callback if provided
                        return true; // Exit successfully
                    }

                    // Check for timeout
                    if ((DateTime.Now - startTime).TotalMilliseconds > timeoutMilliseconds)
                    {
                        Debug.WriteLine($"Timeout reached while waiting for element '{cssSelector}' to exist.");
                        return false; // Exit if timeout is reached
                    }

                    // Increment the retry counter
                    retryCount++;

                    // Wait for a short period before the next retry
                    await Task.Delay(100); // Adjust delay as needed
                }

                // Log if max retries are reached without success
                Debug.WriteLine($"Max retries reached ({maxRetries}) for element '{cssSelector}'.");
                return false; // Exit if retries are exhausted
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in WaitForElementToExistAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> WaitForElementToExistAsync(string cssSelector, int timeoutMilliseconds = 5000, int maxRetries = 50)
        {
            var startTime = DateTime.Now;
            var retryCount = 0;
            var script = $@"
                (function waitElement() {{
                    var element = document.querySelector(""{cssSelector}"");
                    return element !== null;
                }})();
            ";

            while (retryCount < maxRetries)
            {
                var result = await _webView.ExecuteScriptAsync(script);
                await Task.Delay(100);

                if (result == "true") // The element exists
                {
                    return true; // Return true if the element exists
                }

                // Check for timeout
                if ((DateTime.Now - startTime).TotalMilliseconds > timeoutMilliseconds)
                {
                    Debug.WriteLine("Waiting for element timed out");
                    return false; // Return false if the element does not exist within the timeout
                }

                // Increment the retry count
                retryCount++;

                // Wait for a short period before checking again
                await Task.Delay(100); // Wait 100 milliseconds before retrying
            }

            Debug.WriteLine($@"Maximum retries reached without finding the element. - {cssSelector}");
            return false; // Return false if max retries are exceeded
        }

        // Function to wait until the navigation is completed
        public Task WaitForNavigationCompleted(WebViewFunctions webFunctions)
        {
            navigationCompletionSource = new TaskCompletionSource<bool>();

            webFunctions.AddNavigationCompletedHandler((sender, e) =>
            {
                if (e.IsSuccess)
                {
                    navigationCompletionSource.SetResult(true);
                }
                else
                {
                    navigationCompletionSource.SetResult(false);
                }
            });

            return navigationCompletionSource.Task;
        }

        public async Task DropdownSelectAsync(string searchBy, string name, string optionValue)
        {
            searchBy = searchBy.ToLower();
            string script;

            switch (searchBy)
            {
                case "id":

                    script = $@"
                        var selectElement = document.getElementById(""{name}"");
                        selectElement.value = '{optionValue}';
                        var event = new Event('change');
                        selectElement.dispatchEvent(event);";
                    await _webView.CoreWebView2.ExecuteScriptAsync(script);
                    await Task.Delay(100);
                    break;

                case "class":
                    script = $@"
                        var selectElement = document.getElementsByClassName(""{name}"");
                        selectElement.value = '{optionValue}';
                        var event = new Event('change');
                        selectElement.dispatchEvent(event);";
                    await _webView.CoreWebView2.ExecuteScriptAsync(script);
                    await Task.Delay(100);
                    break;

                case "name":
                    script = $@"
                        var selectElement = document.querySelector(""[name='{name}']"");
                        selectElement.value = '{optionValue}';
                        var event = new Event('change');
                        selectElement.dispatchEvent(event);";
                    await _webView.CoreWebView2.ExecuteScriptAsync(script);
                    await Task.Delay(100);
                    break;
            }
        }

        public async Task<string> GetElementText(string searchBy, string name, int index = 0)
        {
            searchBy = searchBy.ToLower();
            string script;

            switch (searchBy)
            {
                case "id":
                    script = $@"
                        (function extractContent(){{
                            var element = document.getElementById(""{name}"");
                            var text = element.textContent;
                            return text;
                        }})()
                    ";
                    return await _webView.CoreWebView2.ExecuteScriptAsync(script);
                    ;

                case "class":
                    script = $@"
                        var selectElement = document.getElementsByClassName(""{name}"");
                        const textContent = selectElement ? selectElement.textContent : null;

                        (function extractContent(){{
                            var element = document.getElementsByClassName(""{name}"");
                            var text = element{index}.textContent;
                            return text;
                        }})()
                        ";


                    return await _webView.CoreWebView2.ExecuteScriptAsync(script);

                case "name":
                    script = $@"
                        (function extractContent(){{
                            var element = document.querySelector(""[name='{name}']"");
                            var text = element{index}.textContent;
                            return text;
                        }})()
                    ";
                    return await _webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            return null;
        }

        public async Task<object> ExecuteJavascript(string script, bool withReturn = false)
        {
            try
            {
                if (withReturn)
                {
                    return await _webView.CoreWebView2.ExecuteScriptAsync(script);
                }
                else
                {
                    await _webView.CoreWebView2.ExecuteScriptAsync(script);
                    await Task.Delay(100);
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error executing JavaScript: {ex.Message}");
                throw; // Re-throw the exception after logging
            }
        }

        public async Task CheckBoxAsync(string searchBy, string name)
        {
            searchBy = searchBy.ToLower();
            string script;

            switch (searchBy)
            {
                case "id":
                    script = $@"
                var selectElement = document.getElementById(""{name}"");
                if (selectElement && selectElement.type === 'checkbox') {{
                    selectElement.checked = true;
                }}";
                    await _webView.CoreWebView2.ExecuteScriptAsync(script);
                    await Task.Delay(100);
                    break;

                case "class":
                    script = $@"
                var selectElements = document.getElementsByClassName(""{name}"");
                if (selectElements.length > 0) {{
                    for (let element of selectElements) {{
                        if (element.type === 'checkbox') {{
                            element.checked = true;
                        }}
                    }}
                }}";
                    await _webView.CoreWebView2.ExecuteScriptAsync(script);
                    await Task.Delay(100);
                    break;

                case "name":
                    script = $@"
                var selectElement = document.querySelector(""[name='{name}']"");
                if (selectElement && selectElement.type === 'checkbox') {{
                    selectElement.checked = true;
                }}";
                    await _webView.CoreWebView2.ExecuteScriptAsync(script);
                    await Task.Delay(100);
                    break;
            }
        }

        public async Task<string> FindCheckBoxNameByText(string searchText)
        {
            // JavaScript script to find the checkbox name based on the search text
            string script = $@"
                (function findCheckBoxName() {{
                    const targetCell = Array.from(document.querySelectorAll('td')).find(cell => cell.textContent.trim() === '{searchText}');
                    const parentRow = targetCell ? targetCell.parentElement : null;

                    if (parentRow) {{
                        const checkbox = parentRow.querySelector('input[type=""checkbox""]');
                        return checkbox ? checkbox.name : null;
                    }} else {{
                        return null;
                    }}
                }})();
            ";

            // Execute the JavaScript and get the result
            string result = await _webView.CoreWebView2.ExecuteScriptAsync(script);
            await Task.Delay(100);
            // Deserialize the result to get the checkbox name (or null if not found)
            try
            {
                return JsonConvert.DeserializeObject<string>(result);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error deserializing JSON: {ex.Message}");
                return null; // Return null or handle the error as needed
            }
        }

        public JObject CapturedMessage(CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string message = e.TryGetWebMessageAsString();
                JObject jsonMessage = JObject.Parse(message);

                //var data = jsonMessage["data"];
                //Console.WriteLine($"{jsonMessage}");
                return jsonMessage;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving WebView2 message: {ex.Message}");
                throw ex;
            }
        }

        public void SendDataToWeb(JObject data, string functionName = "")
        {
            try
            {
                // Convert JObject to string (JSON format)
                string jsonData = data.ToString();

                // Send the JSON string to the web page (JavaScript)
                //MessageBox.Show(data.ToString());

                _webView.CoreWebView2.PostWebMessageAsString(jsonData);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SendDataToWeb - {functionName}: " + ex.Message);
                Console.WriteLine($"Error sending data to web: {ex.Message}");
            }
        }

        public async Task SearchToLastNode(string selector, string action)
        {
            string script = $@"
                var parentElement = document.querySelector('{selector}');
                var lastElementInside = parentElement.lastElementChild;
                lastElementInside.{action};
            ";
            await _webView.CoreWebView2.ExecuteScriptAsync(script);
            await Task.Delay(100);
        }

        public async Task<bool> WaitForContentToExistAsync(string searchBy, string name, string inputText, int classIndex = 0)
        {
            searchBy = searchBy.ToLower();
            string script;
            bool result = false;

            switch (searchBy)
            {
                case "id":
                    script = $@"
                (function() {{
                    let element = document.getElementById('{name}');
                    if (element) {{
                        element.value = `{inputText}`;
                        return true;
                    }}
                    return false;
                }})();";
                    result = Convert.ToBoolean(await _webView.CoreWebView2.ExecuteScriptAsync(script));
                    break;

                case "class":
                    script = $@"
                (function() {{
                    let elements = document.getElementsByClassName('{name}');
                    if (elements && elements[{classIndex}]) {{
                        elements[{classIndex}].value = `{inputText}`;
                        return true;
                    }}
                    return false;
                }})();";
                    result = Convert.ToBoolean(await _webView.CoreWebView2.ExecuteScriptAsync(script));
                    break;

                case "name":
                    script = $@"
                (function() {{
                    let element = document.querySelector('[name=""{name}""]');
                    if (element) {{
                        element.value = `{inputText}`;
                        return true;
                    }}
                    return false;
                }})();";
                    result = Convert.ToBoolean(await _webView.CoreWebView2.ExecuteScriptAsync(script));
                    break;
            }
            return result;
        }

        public async Task PressKey(string key)
        {
            string script = $@"
                var event = new KeyboardEvent('keydown', {{
                    key: '{key}',
                    code: '{key}',
                    keyCode: {key.GetHashCode()}, // You can customize this if needed
                    bubbles: true
                }});
                document.dispatchEvent(event);
            ";

            await ExecuteJavascript(script);
            await Task.Delay(100);
        }

        public async Task<bool> CheckForAlerts(int timeoutMilliseconds = 5000)
        {
            var startTime = DateTime.Now;
            string script = $@"
            function isAlertOpen() {{
                return window.alert && window.alert.toString().indexOf('native code') !== -1;
            }}
            isAlertOpen();
            ";
            while (true)
            {
                var result = await _webView.ExecuteScriptAsync(script);
                await Task.Delay(100);

                if (result == "true") // The element exists
                {
                    return true; // Return true if the element exists
                }

                // Check for timeout
                if ((DateTime.Now - startTime).TotalMilliseconds > timeoutMilliseconds)
                {
                    Debug.WriteLine("Waiting for element timed out");
                    return false; // Return false if the element does not exist within the timeout
                }

                // Wait for a short period before checking again
                await Task.Delay(100); // Wait 100 milliseconds before retrying
            }
        }

        public async Task<bool> IsPageLoadingAsync()
        {
            try
            {
                var script = "document.readyState"; // JavaScript to check page state
                var result = await _webView.ExecuteScriptAsync(script);
                await Task.Delay(100);

                // Trim quotes and check the state
                result = result.Trim('"');
                if (result == "complete")
                {
                    Debug.WriteLine("Page is fully loaded.");
                    return false; // Page is not loading
                }
                else
                {
                    Debug.WriteLine($"Page is still loading. Current state: {result}");
                    return true; // Page is still loading
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in IsPageLoadingAsync: {ex.Message}");
                return true; // Default to true to ensure it waits if an error occurs
            }
        }

        public void AddScriptDialogOpening(EventHandler<CoreWebView2ScriptDialogOpeningEventArgs> handler)
        {
            _webView.CoreWebView2.ScriptDialogOpening += handler;
            ScriptDialogOpening += handler;
            Debug.WriteLine("ScriptDialogOpening handler added");
        }

        public void RemoveScriptDialogOpening(EventHandler<CoreWebView2ScriptDialogOpeningEventArgs> handler = null)
        {
            _webView.CoreWebView2.ScriptDialogOpening -= handler;
            ScriptDialogOpening -= handler;
            Debug.WriteLine("ScriptDialogOpening handler removed");

        }

        //SAMPLE USAGE
        //LoadPageAsync AreDefaultScriptDialogsEnabled must be false
        private void SampleScriptDialogHandling()
        {
            string errorMessage;
            AddScriptDialogOpening((sender, args) =>
            {
                Console.WriteLine($"Dialog message: {args.Message}");
                errorMessage = args.Message;
                RemoveScriptDialogOpening();
            });
        }

        public void AddDownloadStartingHandler(EventHandler<CoreWebView2DownloadStartingEventArgs> handler)
        {
            _webView.CoreWebView2.DownloadStarting += handler;
            DownloadStarting += handler;
            Debug.WriteLine("Download Starting handler added");
        }

        public void RemoveDownloadStartingHandler(EventHandler<CoreWebView2DownloadStartingEventArgs> handler)
        {
            _webView.CoreWebView2.DownloadStarting -= handler;
            DownloadStarting -= handler;
            Debug.WriteLine("Download Starting handler removed");
        }

        public async Task HardRefresh()
        {
            await _webView.ExecuteScriptAsync("window.location.reload(true)");
        }

        public async Task ClearAllBrowsingDataAsync()
        {
            await _webView.CoreWebView2.CallDevToolsProtocolMethodAsync(
                "Network.setCacheDisabled",
                "{\"cacheDisabled\":true}"
            );

            _webView.CoreWebView2.CookieManager.DeleteAllCookies();
            await _webView.CoreWebView2.Profile.ClearBrowsingDataAsync();
        }

        public void BackFunction()
        {
            _webView.GoBack();
        }

        public void DisposeWebView()
        {
            _webView.CoreWebView2.Stop();
            _webView.Dispose();
        }

        public async Task<bool> HasChildrenAsync(string searchBy, string name, int classIndex = 0)
        {
            searchBy = searchBy.ToLower();
            string script = "";
            string jsResult;
            bool result = false;

            switch (searchBy)
            {
                case "id":
                    script = $@"
                (function() {{
                    const el = document.getElementById('{name}');
                    return el ? el.children.length > 0 : false;
                }})();";
                    break;

                case "class":
                    script = $@"
                (function() {{
                    const els = document.getElementsByClassName('{name}');
                    if (els.length > {classIndex}) {{
                        return els[{classIndex}].children.length > 0;
                    }}
                    return false;
                }})();";
                    break;

                case "name":
                    script = $@"
                (function() {{
                    const els = document.getElementsByName('{name}');
                    if (els.length > 0) {{
                        return els[0].children.length > 0;
                    }}
                    return false;
                }})();";
                    break;

                default:
                    return false;
            }

            jsResult = await _webView.CoreWebView2.ExecuteScriptAsync(script);
            // WebView2 returns JSON strings like "true" or "false", including quotes
            result = jsResult.Trim().ToLower().Contains("true");
            return result;
        }
    }
}
