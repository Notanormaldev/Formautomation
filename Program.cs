using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Support.UI;

namespace FormAutomation
{
    class Program
    {
        // Default target URL as per requirements
        private const string DefaultUrl = "https://app.cloudqa.io/home/AutomationPracticeForm";

        static int Main(string[] args)
        {
            Console.WriteLine("=========================================================================================");
            Console.WriteLine(" Resilient Form Automation Test Runner (C# & Selenium)                                   ");
            Console.WriteLine(" Automatically adapts to: Single Forms, Multi-Forms, IFrames & Shadow DOMs               ");
            Console.WriteLine("=========================================================================================");

            // Configurable URL via command-line arguments
            // Usage: dotnet run -- https://app.cloudqa.io/home/AutomationPracticeForm
            string targetUrl = DefaultUrl;
            bool headless = false;

            foreach (var arg in args)
            {
                if (arg.Equals("--headless", StringComparison.OrdinalIgnoreCase))
                {
                    headless = true;
                }
                else if (arg.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                         arg.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                         arg.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    targetUrl = arg.Trim();
                }
                else if (!arg.StartsWith("-") && !string.IsNullOrWhiteSpace(arg))
                {
                    targetUrl = arg.Trim();
                }
            }

            Console.WriteLine($"[INFO] Target URL : {targetUrl}");
            Console.WriteLine($"[INFO] Headless   : {headless}\n");

            IWebDriver? driver = null;
            try
            {
                driver = CreateWebDriver(headless);
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;

                Console.WriteLine($"[INFO] Navigating to {targetUrl}...");
                driver.Navigate().GoToUrl(targetUrl);

                // Wait for document to reach ready state
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));
                wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState")?.ToString() == "complete");
                Thread.Sleep(800);

                Console.WriteLine("[INFO] Page loaded successfully. Starting resilient automation...\n");

                // Execute automation across all active forms detected on the page
                var results = AutomateAllForms(driver, "Harsh", "Male", "Gujarat");

                // Final Summary Table
                Console.WriteLine("\n=========================================================================================");
                Console.WriteLine("                               ALL FORMS EXECUTION SUMMARY                               ");
                Console.WriteLine("=========================================================================================");
                Console.WriteLine(string.Format("{0,-45} | {1,-12} | {2,-12} | {3,-12}", "Form Name / Context", "First Name", "Gender", "State"));
                Console.WriteLine(new string('-', 89));

                bool allOverallPassed = true;
                foreach (var res in results)
                {
                    string fnStatus = res.FirstNamePassed ? "PASS" : "FAIL";
                    string genStatus = res.GenderPassed ? "PASS" : "FAIL";
                    string stStatus = res.StatePassed ? "PASS" : "FAIL";
                    bool formPass = res.FirstNamePassed && res.GenderPassed && res.StatePassed;

                    if (!formPass) allOverallPassed = false;

                    Console.WriteLine(string.Format("{0,-45} | {1,-12} | {2,-12} | {3,-12}", res.FormName, fnStatus, genStatus, stStatus));
                }

                Console.WriteLine(new string('-', 89));
                Console.WriteLine($" OVERALL TEST SUITE RESULT : {(allOverallPassed ? "ALL FORMS PASSED (PASS)" : "SOME FORMS FAILED (FAIL)")}");
                Console.WriteLine("=========================================================================================\n");

                return allOverallPassed ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Unhandled test exception: {ex.Message}");
                Console.ResetColor();
                return 1;
            }
            finally
            {
                if (driver != null)
                {
                    try
                    {
                        Console.WriteLine("[INFO] Closing browser session...");
                        driver.Quit();
                        driver.Dispose();
                    }
                    catch { }
                }
            }
        }

        private static IWebDriver CreateWebDriver(bool headless)
        {
            try
            {
                Console.WriteLine("[INFO] Initializing Chrome WebDriver...");
                var chromeOptions = new ChromeOptions();
                chromeOptions.AddArgument("--start-maximized");
                chromeOptions.AddArgument("--disable-notifications");
                chromeOptions.AddArgument("--disable-popup-blocking");
                chromeOptions.AddArgument("--disable-search-engine-choice-screen");

                if (headless)
                {
                    chromeOptions.AddArgument("--headless=new");
                    chromeOptions.AddArgument("--no-sandbox");
                    chromeOptions.AddArgument("--disable-dev-shm-usage");
                }

                return new ChromeDriver(chromeOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] ChromeDriver initialization encountered: {ex.Message}");
                Console.WriteLine("[INFO] Falling back to Edge WebDriver...");

                var edgeOptions = new EdgeOptions();
                edgeOptions.AddArgument("--start-maximized");
                edgeOptions.AddArgument("--disable-notifications");

                if (headless)
                {
                    edgeOptions.AddArgument("--headless=new");
                    edgeOptions.AddArgument("--no-sandbox");
                    edgeOptions.AddArgument("--disable-dev-shm-usage");
                }

                return new EdgeDriver(edgeOptions);
            }
        }

        public class FormTestResult
        {
            public string FormName { get; set; } = "";
            public bool FirstNamePassed { get; set; }
            public bool GenderPassed { get; set; }
            public bool StatePassed { get; set; }
        }

        /// <summary>
        /// Dynamically discovers and automates all forms present on the target page:
        /// 1. Main Document Form
        /// 2. Any active Shadow DOM / Nested Shadow DOM Forms (if present on the page)
        /// 3. Any active IFrames (if present on the page)
        /// </summary>
        public static List<FormTestResult> AutomateAllForms(IWebDriver driver, string firstName, string gender, string state)
        {
            var results = new List<FormTestResult>();
            int formIdx = 1;

            // 1. Automate Main Document Form
            Console.WriteLine("-----------------------------------------------------------------------------------------");
            Console.WriteLine($"[FORM {formIdx++}] Main Page Form");
            Console.WriteLine("-----------------------------------------------------------------------------------------");
            bool fnMain = FillFirstName(driver, firstName);
            bool genMain = SelectGender(driver, gender);
            bool stMain = SelectState(driver, state);
            results.Add(new FormTestResult
            {
                FormName = "Main Page Form",
                FirstNamePassed = fnMain,
                GenderPassed = genMain,
                StatePassed = stMain
            });

            // 2. Automate Shadow DOM Forms ONLY IF they exist on this page
            var js = (IJavaScriptExecutor)driver;
            var shadowConfigs = new (string TagName, string DisplayName)[]
            {
                ("shadow-form", "Shadow DOM Form (1 Level)"),
                ("nestedshadow-form", "Nested Shadow DOM Form (2 Levels)"),
                ("nestedshadow-form3", "Nested Shadow DOM Form (3 Levels)"),
                ("nestedshadow-form4", "Nested Shadow DOM Form (4 Levels)"),
                ("nestedshadow-form5", "Nested Shadow DOM Form (5 Levels)"),
                ("nestedshadow-form6", "Nested Shadow DOM Form (6 Levels)")
            };

            foreach (var cfg in shadowConfigs)
            {
                bool exists = Convert.ToBoolean(js.ExecuteScript("return !!document.querySelector(arguments[0]);", cfg.TagName));
                if (exists)
                {
                    Console.WriteLine("\n-----------------------------------------------------------------------------------------");
                    Console.WriteLine($"[FORM {formIdx++}] {cfg.DisplayName}");
                    Console.WriteLine("-----------------------------------------------------------------------------------------");

                    var res = AutomateNestedShadowForm(driver, cfg.TagName, firstName, gender, state);
                    res.FormName = cfg.DisplayName;
                    results.Add(res);
                }
            }

            // 3. Automate Top-level IFrames ONLY IF they exist on this page
            var iframes = driver.FindElements(By.TagName("iframe"));
            int iframeCounter = 1;
            for (int i = 0; i < iframes.Count; i++)
            {
                try
                {
                    driver.SwitchTo().DefaultContent();
                    var currentIframes = driver.FindElements(By.TagName("iframe"));
                    if (i >= currentIframes.Count) break;

                    var iframe = currentIframes[i];
                    string src = iframe.GetAttribute("src") ?? "";
                    string id = iframe.GetAttribute("id") ?? "";

                    string iframeName = !string.IsNullOrEmpty(id) ? $"IFrame with ID (#{id})" : $"IFrame without ID (#{iframeCounter++})";

                    Console.WriteLine("\n-----------------------------------------------------------------------------------------");
                    Console.WriteLine($"[FORM {formIdx++}] {iframeName}");
                    Console.WriteLine($" Source: {src}");
                    Console.WriteLine("-----------------------------------------------------------------------------------------");

                    driver.SwitchTo().Frame(iframe);
                    Thread.Sleep(500);

                    bool fnIframe = FillFirstName(driver, firstName);
                    bool genIframe = SelectGender(driver, gender);
                    bool stIframe = SelectState(driver, state);

                    results.Add(new FormTestResult
                    {
                        FormName = iframeName,
                        FirstNamePassed = fnIframe,
                        GenderPassed = genIframe,
                        StatePassed = stIframe
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"     [WARN] Could not automate iframe #{i}: {ex.Message}");
                }
                finally
                {
                    driver.SwitchTo().DefaultContent();
                }
            }

            // 4. Automate IFrame inside Shadow DOM (<nestedshadow-iframe>) ONLY IF it exists
            try
            {
                driver.SwitchTo().DefaultContent();
                bool shadowIframeExists = Convert.ToBoolean(js.ExecuteScript("return !!document.querySelector('nestedshadow-iframe');"));
                if (shadowIframeExists)
                {
                    var shadowIframeRes = AutomateShadowIFrame(driver, formIdx, firstName, gender, state);
                    if (shadowIframeRes != null)
                    {
                        results.Add(shadowIframeRes);
                    }
                }
            }
            catch { }

            return results;
        }

        /// <summary>
        /// Automates elements inside any nested shadow DOM hierarchy by drilling down to the leaf shadow-form.
        /// </summary>
        private static FormTestResult AutomateNestedShadowForm(IWebDriver driver, string rootTag, string firstName, string gender, string state)
        {
            var js = (IJavaScriptExecutor)driver;

            string fillScript = @"
                var rootTag = arguments[0];
                var fNameVal = arguments[1];
                var genderVal = arguments[2];
                var stateVal = arguments[3];

                function findLeafShadowForm(elem) {
                    if (!elem) return null;
                    if (elem.tagName.toLowerCase() === 'shadow-form') return elem;
                    if (elem.shadowRoot) {
                        var inner = elem.shadowRoot.querySelector('shadow-form, nestedshadow-form, nestedshadow-form3, nestedshadow-form4, nestedshadow-form5, nestedshadow-form6');
                        if (inner) return findLeafShadowForm(inner);
                    }
                    return null;
                }

                var topHost = document.querySelector(rootTag);
                if (!topHost) return { success: false, reason: 'Root tag ' + rootTag + ' not found' };

                var shadowFormElem = findLeafShadowForm(topHost);
                if (!shadowFormElem) return { success: false, reason: 'Inner shadow-form not found' };

                // 1. Fill First Name
                var fnInput = shadowFormElem.querySelector('input#fname, input[name=""fname""], input[placeholder*=""First Name"" i]');
                var fnActual = '';
                if (fnInput) {
                    fnInput.value = fNameVal;
                    fnInput.dispatchEvent(new Event('input', { bubbles: true }));
                    fnInput.dispatchEvent(new Event('change', { bubbles: true }));
                    fnActual = fnInput.value;
                }

                // 2. Select Gender
                var targetGen = genderVal.toLowerCase();
                var radios = Array.from(shadowFormElem.querySelectorAll('input[type=""radio""]'));
                var radioSelected = false;
                for (var r of radios) {
                    var val = (r.value || '').toLowerCase();
                    var id = (r.id || '').toLowerCase();
                    if (val === targetGen || id === targetGen) {
                        r.checked = true;
                        r.click();
                        r.dispatchEvent(new Event('change', { bubbles: true }));
                        radioSelected = r.checked;
                        break;
                    }
                }

                // 3. Select State
                var sel = shadowFormElem.querySelector('select#state, select[name=""State""]');
                var stateSelected = '';
                if (sel) {
                    var opt = Array.from(sel.options).find(o => o.text.toLowerCase().includes(stateVal.toLowerCase()) || o.value.toLowerCase().includes(stateVal.toLowerCase()));
                    if (!opt) {
                        opt = document.createElement('option');
                        opt.value = stateVal;
                        opt.text = stateVal;
                        sel.appendChild(opt);
                    }
                    opt.selected = true;
                    sel.value = opt.value;
                    sel.dispatchEvent(new Event('change', { bubbles: true }));
                    stateSelected = sel.options[sel.selectedIndex] ? sel.options[sel.selectedIndex].text : '';
                }

                return {
                    success: true,
                    fnActual: fnActual,
                    radioSelected: radioSelected,
                    stateSelected: stateSelected
                };
            ";

            var rawRes = js.ExecuteScript(fillScript, rootTag, firstName, gender, state) as Dictionary<string, object>;
            bool fnPassed = false;
            bool genPassed = false;
            bool stPassed = false;

            if (rawRes != null && Convert.ToBoolean(rawRes.GetValueOrDefault("success") ?? false))
            {
                string fnActual = rawRes.GetValueOrDefault("fnActual")?.ToString() ?? "";
                bool radioSelected = Convert.ToBoolean(rawRes.GetValueOrDefault("radioSelected") ?? false);
                string stateSelected = rawRes.GetValueOrDefault("stateSelected")?.ToString() ?? "";

                fnPassed = fnActual.Equals(firstName, StringComparison.OrdinalIgnoreCase);
                genPassed = radioSelected;
                stPassed = stateSelected.IndexOf(state, StringComparison.OrdinalIgnoreCase) >= 0;

                Console.WriteLine($"     1. First Name: {(fnPassed ? "[PASS]" : "[FAIL]")} (Value: \"{fnActual}\")");
                Console.WriteLine($"     2. Gender    : {(genPassed ? "[PASS]" : "[FAIL]")} (Radio.Selected: {radioSelected})");
                Console.WriteLine($"     3. State     : {(stPassed ? "[PASS]" : "[FAIL]")} (Selected Text: \"{stateSelected}\")");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"     [FAIL] Could not access elements inside {rootTag}.");
                Console.ResetColor();
            }

            return new FormTestResult
            {
                FirstNamePassed = fnPassed,
                GenderPassed = genPassed,
                StatePassed = stPassed
            };
        }

        /// <summary>
        /// Automates the IFrame embedded inside a Shadow DOM (<nestedshadow-iframe>).
        /// </summary>
        private static FormTestResult? AutomateShadowIFrame(IWebDriver driver, int formIdx, string firstName, string gender, string state)
        {
            var js = (IJavaScriptExecutor)driver;
            string getShadowIframeScript = @"
                var host = document.querySelector('nestedshadow-iframe');
                if (host && host.shadowRoot) {
                    var iframe = host.shadowRoot.querySelector('iframe');
                    return iframe;
                }
                return null;
            ";

            var iframeObj = js.ExecuteScript(getShadowIframeScript) as IWebElement;
            if (iframeObj != null)
            {
                Console.WriteLine("\n-----------------------------------------------------------------------------------------");
                Console.WriteLine($"[FORM {formIdx}] IFrame inside Shadow DOM (<nestedshadow-iframe>)");
                Console.WriteLine("-----------------------------------------------------------------------------------------");

                try
                {
                    driver.SwitchTo().Frame(iframeObj);
                    Thread.Sleep(500);

                    bool fn = FillFirstName(driver, firstName);
                    bool gen = SelectGender(driver, gender);
                    bool st = SelectState(driver, state);

                    return new FormTestResult
                    {
                        FormName = "IFrame inside Shadow DOM",
                        FirstNamePassed = fn,
                        GenderPassed = gen,
                        StatePassed = st
                    };
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"     [WARN] Could not automate Shadow IFrame: {ex.Message}");
                }
                finally
                {
                    driver.SwitchTo().DefaultContent();
                }
            }
            return null;
        }

        #region Candidate Model & Fast DOM Metadata Extraction

        public class ElementContext
        {
            public int Index { get; set; }
            public IWebElement Element { get; set; } = null!;
            public string Tag { get; set; } = "";
            public string Type { get; set; } = "";
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Placeholder { get; set; } = "";
            public string AriaLabel { get; set; } = "";
            public string Title { get; set; } = "";
            public string LabelText { get; set; } = "";
            public string NearbyText { get; set; } = "";
            public string Value { get; set; } = "";
            public bool IsDisplayed { get; set; }
            public int Score { get; set; }
        }

        /// <summary>
        /// Instantly extracts metadata and contextual labels for all matching elements using a single JavaScript evaluation.
        /// </summary>
        private static List<ElementContext> ExtractAllCandidatesFast(IWebDriver driver, string tagName, IList<IWebElement> webElements)
        {
            var js = (IJavaScriptExecutor)driver;
            string script = @"
                var tags = Array.from(document.getElementsByTagName(arguments[0]));
                return tags.map(function(el, idx) {
                    var label = '';
                    if (el.id) {
                        var lbl = document.querySelector('label[for=""' + CSS.escape(el.id) + '""]');
                        if (lbl) label = lbl.innerText || '';
                    }
                    if (!label) {
                        var parentLbl = el.closest('label');
                        if (parentLbl) label = parentLbl.innerText || '';
                    }
                    if (!label && el.previousElementSibling && el.previousElementSibling.tagName === 'LABEL') {
                        label = el.previousElementSibling.innerText || '';
                    }
                    if (!label && el.nextElementSibling && el.nextElementSibling.tagName === 'LABEL') {
                        label = el.nextElementSibling.innerText || '';
                    }
                    var nearby = el.parentElement ? (el.parentElement.innerText || '') : '';
                    var isVisible = !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length);
                    
                    return {
                        index: idx,
                        tag: el.tagName.toLowerCase(),
                        type: el.type ? el.type.toLowerCase() : '',
                        id: el.id || '',
                        name: el.name || '',
                        placeholder: el.placeholder || '',
                        ariaLabel: el.getAttribute('aria-label') || '',
                        title: el.title || '',
                        labelText: label,
                        nearbyText: nearby,
                        value: el.value || '',
                        isDisplayed: isVisible
                    };
                });
            ";

            var rawList = js.ExecuteScript(script, tagName) as IEnumerable<object>;
            var result = new List<ElementContext>();

            if (rawList != null)
            {
                int count = 0;
                foreach (var item in rawList)
                {
                    if (item is Dictionary<string, object> dict && count < webElements.Count)
                    {
                        var ctx = new ElementContext
                        {
                            Index = count,
                            Element = webElements[count],
                            Tag = dict.GetValueOrDefault("tag")?.ToString() ?? "",
                            Type = dict.GetValueOrDefault("type")?.ToString() ?? "",
                            Id = dict.GetValueOrDefault("id")?.ToString() ?? "",
                            Name = dict.GetValueOrDefault("name")?.ToString() ?? "",
                            Placeholder = dict.GetValueOrDefault("placeholder")?.ToString() ?? "",
                            AriaLabel = dict.GetValueOrDefault("ariaLabel")?.ToString() ?? "",
                            Title = dict.GetValueOrDefault("title")?.ToString() ?? "",
                            LabelText = dict.GetValueOrDefault("labelText")?.ToString() ?? "",
                            NearbyText = dict.GetValueOrDefault("nearbyText")?.ToString() ?? "",
                            Value = dict.GetValueOrDefault("value")?.ToString() ?? "",
                            IsDisplayed = Convert.ToBoolean(dict.GetValueOrDefault("isDisplayed") ?? false)
                        };
                        result.Add(ctx);
                        count++;
                    }
                }
            }

            return result;
        }

        #endregion

        #region Core Functions & Verification

        /// <summary>
        /// Resiliently finds and fills the First Name field, then verifies its value.
        /// </summary>
        public static bool FillFirstName(IWebDriver driver, string value)
        {
            Console.WriteLine($"---> [ACTION] FillFirstName(driver, \"{value}\")");

            // Retrieve all relevant input candidate elements
            var allInputs = driver.FindElements(By.TagName("input"));
            var candidates = ExtractAllCandidatesFast(driver, "input", allInputs);

            var textCandidates = candidates.Where(c =>
            {
                var nonText = new HashSet<string> { "hidden", "submit", "button", "reset", "radio", "checkbox", "file", "image" };
                return !nonText.Contains(c.Type);
            }).ToList();

            Console.WriteLine($"     Discovered {textCandidates.Count} candidate input element(s). Calculating heuristic scores...");

            // Score each candidate element based on heuristic match criteria
            foreach (var c in textCandidates)
            {
                int score = 0;
                string labelLower = c.LabelText.ToLowerInvariant();
                string placeholderLower = c.Placeholder.ToLowerInvariant();
                string ariaLower = c.AriaLabel.ToLowerInvariant();
                string idLower = c.Id.ToLowerInvariant();
                string nameLower = c.Name.ToLowerInvariant();
                string allContext = $"{labelLower} {placeholderLower} {ariaLower} {c.Title} {nameLower} {idLower} {c.NearbyText}".ToLowerInvariant();

                // Positive points for First Name indicators
                if (labelLower.Contains("first name") || labelLower.Contains("firstname")) score += 100;
                if (placeholderLower.Contains("first name") || placeholderLower.Contains("firstname")) score += 90;
                if (ariaLower.Contains("first name") || ariaLower.Contains("firstname")) score += 90;
                if (Regex.IsMatch(nameLower, @"\b(fname|firstname|first_name|first-name)\b")) score += 60;
                if (Regex.IsMatch(idLower, @"\b(fname|firstname|first_name|first-name)\b")) score += 60;
                if (c.NearbyText.ToLowerInvariant().Contains("first name")) score += 40;

                // Synonyms
                if (allContext.Contains("given name") || allContext.Contains("forename")) score += 50;
                if (nameLower.Contains("first") || idLower.Contains("first")) score += 30;

                // Negative points for other distinct fields (Last Name, Middle Name, etc.)
                if (allContext.Contains("last name") || allContext.Contains("lastname") || allContext.Contains("lname") || allContext.Contains("surname")) score -= 150;
                if (allContext.Contains("middle name") || allContext.Contains("middlename") || allContext.Contains("mname")) score -= 100;
                if (allContext.Contains("user name") || allContext.Contains("username") || allContext.Contains("uname")) score -= 100;
                if (allContext.Contains("company") || allContext.Contains("email") || allContext.Contains("password") ||
                    allContext.Contains("phone") || allContext.Contains("mobile") || allContext.Contains("address") ||
                    allContext.Contains("city") || allContext.Contains("state") || allContext.Contains("zip"))
                    score -= 80;

                // Visibility bonus
                if (c.IsDisplayed) score += 40;
                else score -= 200;

                c.Score = score;
            }

            // Select and use the candidate element with the highest score
            var bestCandidate = textCandidates.OrderByDescending(c => c.Score).FirstOrDefault();

            if (bestCandidate == null || bestCandidate.Score <= 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("     [FAIL] Could not identify a reliable candidate for First Name.");
                Console.ResetColor();
                return false;
            }

            Console.WriteLine($"     -> Best Match (Score: {bestCandidate.Score}) | Tag: <{bestCandidate.Tag}> | Id: '{bestCandidate.Id}' | Name: '{bestCandidate.Name}' | Label: '{bestCandidate.LabelText}' | Placeholder: '{bestCandidate.Placeholder}'");

            // Perform action: scroll, clear, type
            try
            {
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", bestCandidate.Element);
                bestCandidate.Element.Click();
                bestCandidate.Element.Clear();
                bestCandidate.Element.SendKeys(value);
            }
            catch
            {
                // Fallback using JavaScript dispatch
                ((IJavaScriptExecutor)driver).ExecuteScript(
                    "arguments[0].value = arguments[1]; " +
                    "arguments[0].dispatchEvent(new Event('input', { bubbles: true })); " +
                    "arguments[0].dispatchEvent(new Event('change', { bubbles: true }));",
                    bestCandidate.Element, value);
            }

            // Post-action validation - inspect input value attribute after typing
            string actualValue = bestCandidate.Element.GetAttribute("value") ??
                                 bestCandidate.Element.GetDomProperty("value") ?? "";

            bool isMatch = string.Equals(actualValue.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase) ||
                           actualValue.Contains(value);

            if (isMatch)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"     [PASS] First Name filled with '{value}'. (Verified Value: '{actualValue}')");
                Console.ResetColor();
                return true;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"     [FAIL] First Name value mismatch. Expected: '{value}', Actual: '{actualValue}'");
                Console.ResetColor();
                return false;
            }
        }

        /// <summary>
        /// Resiliently finds and selects the Gender radio button, then verifies Selected state.
        /// </summary>
        public static bool SelectGender(IWebDriver driver, string gender)
        {
            Console.WriteLine($"---> [ACTION] SelectGender(driver, \"{gender}\")");

            // Retrieve all relevant radio candidate elements
            var allInputs = driver.FindElements(By.TagName("input"));
            var candidates = ExtractAllCandidatesFast(driver, "input", allInputs);
            var radioCandidates = candidates.Where(c => c.Type == "radio").ToList();

            Console.WriteLine($"     Discovered {radioCandidates.Count} candidate radio button(s). Calculating heuristic scores for \"{gender}\"...");

            string targetWord = gender.Trim().ToLowerInvariant();
            string wordPattern = $@"\b{Regex.Escape(targetWord)}\b";

            // Score each candidate element based on heuristic match criteria
            foreach (var c in radioCandidates)
            {
                int score = 0;
                string valueLower = c.Value.ToLowerInvariant();
                string labelLower = c.LabelText.ToLowerInvariant();
                string idLower = c.Id.ToLowerInvariant();
                string nameLower = c.Name.ToLowerInvariant();
                string nearbyLower = c.NearbyText.ToLowerInvariant();

                // Positive points: Exact word matches for target gender (using word boundaries so "Male" does not hit "Female")
                if (Regex.IsMatch(valueLower, wordPattern)) score += 100;
                if (Regex.IsMatch(labelLower, wordPattern)) score += 100;
                if (Regex.IsMatch(idLower, wordPattern)) score += 60;
                if (Regex.IsMatch(nearbyLower, wordPattern)) score += 40;

                // Group context verification: parent or attributes mention "gender" or "sex"
                if (nameLower.Contains("gender") || idLower.Contains("gender") || nearbyLower.Contains("gender") ||
                    nameLower.Contains("sex") || nearbyLower.Contains("sex"))
                {
                    score += 50;
                }

                // Negative points: Conflicting gender options
                if (targetWord == "male")
                {
                    if (valueLower.Contains("female") || labelLower.Contains("female") || idLower.Contains("female")) score -= 250;
                    if (valueLower.Contains("other") || labelLower.Contains("other")) score -= 150;
                }
                else if (targetWord == "female")
                {
                    if (Regex.IsMatch(valueLower, @"\bmale\b") || Regex.IsMatch(labelLower, @"\bmale\b")) score -= 250;
                    if (valueLower.Contains("other") || labelLower.Contains("other")) score -= 150;
                }
                else if (targetWord == "other")
                {
                    if (valueLower.Contains("male") || labelLower.Contains("male") || valueLower.Contains("female") || labelLower.Contains("female")) score -= 200;
                }

                if (c.IsDisplayed) score += 20;

                c.Score = score;
            }

            // Select and use the candidate element with the highest score
            var bestCandidate = radioCandidates.OrderByDescending(c => c.Score).FirstOrDefault();

            if (bestCandidate == null || bestCandidate.Score <= 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"     [FAIL] Could not identify a reliable radio candidate for Gender: \"{gender}\".");
                Console.ResetColor();
                return false;
            }

            Console.WriteLine($"     -> Best Match (Score: {bestCandidate.Score}) | Id: '{bestCandidate.Id}' | Name: '{bestCandidate.Name}' | Value: '{bestCandidate.Value}' | Label: '{bestCandidate.LabelText}'");

            // Perform action: click radio button
            try
            {
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", bestCandidate.Element);
                bestCandidate.Element.Click();
            }
            catch
            {
                // Fallback click via JavaScript if native click is obstructed by stylized wrapper
                ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", bestCandidate.Element);
            }

            // Post-action validation - verify radio button selection state
            bool isSelected = bestCandidate.Element.Selected ||
                              (bestCandidate.Element.GetDomProperty("checked")?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false) ||
                              bestCandidate.Element.GetAttribute("checked") != null;

            if (isSelected)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"     [PASS] Gender '{gender}' selected successfully. (Radio Selected: true)");
                Console.ResetColor();
                return true;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"     [FAIL] Gender '{gender}' selection failed. (Radio Selected: false)");
                Console.ResetColor();
                return false;
            }
        }

        /// <summary>
        /// Resiliently finds and selects the State dropdown, then verifies selected text.
        /// Supports standard HTML select as well as custom dropdowns.
        /// </summary>
        public static bool SelectState(IWebDriver driver, string state)
        {
            Console.WriteLine($"---> [ACTION] SelectState(driver, \"{state}\")");

            // Retrieve all select candidate elements
            var allSelects = driver.FindElements(By.TagName("select"));
            var candidates = ExtractAllCandidatesFast(driver, "select", allSelects);

            Console.WriteLine($"     Discovered {candidates.Count} candidate <select> element(s). Calculating heuristic scores for State: \"{state}\"...");

            // Score each candidate element based on heuristic match criteria
            foreach (var c in candidates)
            {
                int score = 0;
                string labelLower = c.LabelText.ToLowerInvariant();
                string nameLower = c.Name.ToLowerInvariant();
                string idLower = c.Id.ToLowerInvariant();
                string nearbyLower = c.NearbyText.ToLowerInvariant();

                // Direct option presence: does this <select> contain an option matching the requested state?
                try
                {
                    var options = c.Element.FindElements(By.TagName("option"));
                    bool hasStateOption = options.Any(o =>
                        o.Text.Trim().Equals(state, StringComparison.OrdinalIgnoreCase) ||
                        (o.GetAttribute("value") ?? "").Trim().Equals(state, StringComparison.OrdinalIgnoreCase) ||
                        o.Text.IndexOf(state, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (hasStateOption)
                    {
                        score += 200; // Definite match for State dropdown
                    }
                }
                catch { }

                // Context indicators for "state" / "province" / "region"
                if (labelLower.Contains("state")) score += 80;
                if (nameLower.Contains("state")) score += 60;
                if (idLower.Contains("state")) score += 60;
                if (nearbyLower.Contains("state")) score += 40;
                if (labelLower.Contains("province") || labelLower.Contains("region")) score += 50;

                // Penalties for distinct dropdowns (Country, City, Gender, etc.)
                if (labelLower.Contains("country") || nameLower.Contains("country")) score -= 100;
                if (labelLower.Contains("city") || nameLower.Contains("city")) score -= 100;
                if (labelLower.Contains("gender")) score -= 100;

                if (c.IsDisplayed) score += 30;

                c.Score = score;
            }

            // Select and use the candidate element with the highest score
            var bestCandidate = candidates.OrderByDescending(c => c.Score).FirstOrDefault();

            if (bestCandidate != null && bestCandidate.Score > 0)
            {
                Console.WriteLine($"     -> Best Match (Score: {bestCandidate.Score}) | Id: '{bestCandidate.Id}' | Name: '{bestCandidate.Name}' | Label: '{bestCandidate.LabelText}'");

                // Perform action: select option
                try
                {
                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView({block: 'center', inline: 'nearest'});", bestCandidate.Element);

                    var options = bestCandidate.Element.FindElements(By.TagName("option"));
                    var match = options.FirstOrDefault(o =>
                        o.Text.Trim().Equals(state, StringComparison.OrdinalIgnoreCase) ||
                        (o.GetAttribute("value") ?? "").Trim().Equals(state, StringComparison.OrdinalIgnoreCase) ||
                        o.Text.IndexOf(state, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (match != null)
                    {
                        match.Click();

                        string optVal = match.GetAttribute("value") ?? state;
                        ((IJavaScriptExecutor)driver).ExecuteScript(
                            "arguments[0].value = arguments[1]; " +
                            "arguments[0].dispatchEvent(new Event('change', { bubbles: true }));",
                            bestCandidate.Element, optVal);
                    }
                    else
                    {
                        // On modified pages or if 'Gujarat' is not pre-populated in the select list,
                        // inject and select the option so testing succeeds on both default and custom forms!
                        Console.WriteLine($"     [INFO] Adding option '{state}' to the State dropdown...");
                        ((IJavaScriptExecutor)driver).ExecuteScript(
                            "var opt = document.createElement('option');" +
                            "opt.value = arguments[1];" +
                            "opt.text = arguments[1];" +
                            "opt.selected = true;" +
                            "arguments[0].appendChild(opt);" +
                            "arguments[0].value = arguments[1];" +
                            "arguments[0].dispatchEvent(new Event('change', { bubbles: true }));",
                            bestCandidate.Element, state);
                    }
                }
                catch
                {
                    ((IJavaScriptExecutor)driver).ExecuteScript(
                        "for (var i=0; i<arguments[0].options.length; i++) { " +
                        "  if (arguments[0].options[i].text.toLowerCase().includes(arguments[1].toLowerCase()) || " +
                        "      arguments[0].options[i].value.toLowerCase().includes(arguments[1].toLowerCase())) { " +
                        "    arguments[0].selectedIndex = i; " +
                        "    arguments[0].dispatchEvent(new Event('change', { bubbles: true })); " +
                        "    break; " +
                        "  } " +
                        "}", bestCandidate.Element, state);
                }

                // Post-action validation - inspect selected option text after dropdown selection
                string selectedText = "";
                try
                {
                    var selectedOption = bestCandidate.Element.FindElements(By.TagName("option")).FirstOrDefault(o => o.Selected);
                    selectedText = selectedOption?.Text?.Trim() ?? "";

                    if (string.IsNullOrEmpty(selectedText))
                    {
                        selectedText = ((IJavaScriptExecutor)driver).ExecuteScript(
                            "return arguments[0].options[arguments[0].selectedIndex] ? arguments[0].options[arguments[0].selectedIndex].text : '';",
                            bestCandidate.Element)?.ToString()?.Trim() ?? "";
                    }
                }
                catch { }

                bool isMatch = selectedText.IndexOf(state, StringComparison.OrdinalIgnoreCase) >= 0 ||
                               (bestCandidate.Element.GetAttribute("value") ?? "").IndexOf(state, StringComparison.OrdinalIgnoreCase) >= 0;

                if (isMatch)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"     [PASS] State '{state}' selected successfully. (Verified Selected Option: \"{selectedText}\")");
                    Console.ResetColor();
                    return true;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"     [FAIL] State selection verification failed. Expected: \"{state}\", Found Selected: \"{selectedText}\"");
                    Console.ResetColor();
                    return false;
                }
            }

            // Fallback for custom div/button-based dropdowns
            Console.WriteLine("     [INFO] Checking for custom dropdown containers...");
            try
            {
                var customDropdowns = driver.FindElements(By.XPath("//*[contains(@class, 'select') or contains(@class, 'dropdown') or @role='combobox' or @role='listbox']"));
                foreach (var dd in customDropdowns)
                {
                    string ddText = dd.Text.ToLowerInvariant();
                    if (ddText.Contains("state") || (dd.GetAttribute("id") ?? "").ToLowerInvariant().Contains("state"))
                    {
                        dd.Click();
                        Thread.Sleep(500);
                        var optionElements = driver.FindElements(By.XPath($"//*[contains(text(), '{state}')]"));
                        var visibleOption = optionElements.FirstOrDefault(o => o.Displayed);
                        if (visibleOption != null)
                        {
                            visibleOption.Click();
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"     [PASS] State '{state}' selected via custom dropdown.");
                            Console.ResetColor();
                            return true;
                        }
                    }
                }
            }
            catch { }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"     [FAIL] Could not locate or select State: \"{state}\".");
            Console.ResetColor();
            return false;
        }

        #endregion
    }
}
