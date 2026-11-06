# Resilient UI Form Automation (C# & Selenium)

An automated testing solution built in **C# (.NET 10)** using **Selenium WebDriver** designed to fulfill **Task 1: Automation that keeps working when the page changes**.

The automation targets three core inputs:
1. **First Name** (Text box)
2. **Gender** (Radio button: `"Male"`)
3. **State** (Dropdown: `"Gujarat"`)

---

## 🎯 Key Objectives & Robustness

Traditional test scripts break as soon as element IDs, classes, XPaths, or field orders change. This project implements a **self-healing, heuristic-scoring element discovery engine**:
- **No brittle hardcoded XPaths or CSS IDs**: Fields are identified through natural language cues (labels, placeholders, aria attributes, proximity).
- **Immune to Layout & Reordering Changes**: Last Name can appear before First Name, or fields can move across containers without affecting accuracy.
- **Deep DOM Penetration**: Traverses the main DOM, nested Shadow DOMs (up to 6 levels deep), and embedded IFrames automatically.
- **Zero-Delay JS Evaluation**: Extracts element metadata instantly without triggering WebDriver polling timeouts.
- **Configurable URL**: Accepts any target URL via command-line arguments, defaulting to `https://app.cloudqa.io/home/AutomationPracticeForm`.

---

## 🚀 Getting Started

### Prerequisites
- [.NET 8.0 SDK or .NET 10.0 SDK](https://dotnet.microsoft.com/download)
- Google Chrome browser (or Microsoft Edge as an automatic fallback)

### How to Run

1. **Default Run** (uses default CloudQA practice form):
   ```powershell
   dotnet run
   ```

2. **Custom / Modified Page Run** (Step 3 requirement):
   ```powershell
   dotnet run -- https://app.cloudqa.io/home/AutomationPracticeForm
   ```

3. **Local Modified Test Page** (provided in repo for verification):
   ```powershell
   dotnet run -- "file:///$(pwd)/sample_modified_page.html"
   ```

4. **Headless Execution** (optional, for CI/CD environments):
   ```powershell
   dotnet run -- https://app.cloudqa.io/home/AutomationPracticeForm --headless
   ```

---

## 🧠 Approach & Architecture

### 1. Context Extraction (`ExtractAllCandidatesFast`)
Instead of issuing individual WebDriver round-trips for every candidate, a single lightweight JavaScript routine runs on the active document/root to extract:
- Associated `<label>` text (via `for` matching, wrapping `<label>`, or preceding/following sibling nodes).
- `placeholder`, `aria-label`, `title`, and surrounding container text.
- Field visibility (`isDisplayed`) and type.

### 2. Heuristic Scoring Model
Each candidate is evaluated through positive weights and negative penalties:
- **First Name**:
  - `+100` for "First Name" in label, `+90` in placeholder / aria-label.
  - `+60` for `fname`, `first_name`, or synonyms like `given name` / `forename`.
  - `-150` for `last name`, `-100` for `middle name` / `username` / `email`.
- **Gender (Male)**:
  - Exact word boundary matching (`\bmale\b`) so "Male" never accidentally triggers on "Female".
  - `+50` for surrounding container referencing "gender" or "sex".
  - `-250` penalty for elements explicitly labeled "Female" or "Other".
- **State (Gujarat)**:
  - `+200` bonus if the `<select>` contains an `<option>` for "Gujarat".
  - `+80` for labels containing "state", "province", or "region".
  - `-100` for dropdowns representing "country" or "city".
  - Seamless fallback to custom div/button-based dropdown components.

### 3. Step 5: Post-Action Verification & Assertion
- **Text box**: Reads `element.GetAttribute("value")` and asserts equality to `"Harsh"`.
- **Radio button**: Reads `element.Selected` (and DOM property `checked`) to verify selection.
- **Dropdown**: Inspects the currently selected `<option>` text and asserts `"Gujarat"`.
- Reports a clear `[PASS]` or `[FAIL]` status for each field and prints a summary scorecard.

---

## ⚖️ Limitations & Honest Trade-offs

1. **Multi-Lingual Forms**:
   - The heuristic dictionary currently prioritizes English keywords (`First Name`, `Given Name`, `Gender`, `State`, etc.). Supporting multilingual forms (e.g. Spanish, German, Hindi) would require localized synonym dictionaries or an embedded embedding/NLP classifier.

2. **Cross-Origin Restricted IFrames**:
   - In cross-origin `iframe` environments governed by strict CORS headers, browser security prevents cross-domain DOM access unless the driver explicitly switches to that frame context.

3. **Dynamic Canvas / WebGL UI**:
   - The engine relies on semantic HTML DOM elements. Canvas-rendered forms (such as Flutter Web canvas kit) do not expose standard DOM input trees without accessibility tree bridges.

---

## 📁 Repository Structure
```
Formautomation/
│
├── Program.cs                 # Main test runner with resilient scoring engine
├── Formautomation.csproj       # Project file with Selenium.WebDriver package
├── sample_modified_page.html  # Modified test page (scrambled IDs, swapped layout)
└── README.md                  # Setup, approach, limitations, and run instructions
```

