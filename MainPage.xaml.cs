using Antlr4.Runtime;
using lab1_calc;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Layouts;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.RegularExpressions;
using Windows.Foundation.Collections;
using Windows.UI.WebUI;
using CalculatorLexer = lab1_calc.lab1_calcLexer;
using CalculatorParser = lab1_calc.lab1_calcParser;
using CalculatorVisitor = lab1_calc.lab1_calcVisitor;
using Grid = Microsoft.Maui.Controls.Grid;
namespace LogicalSpreadSheet
{
    public partial class MainPage : ContentPage
    {
        private class SpreadsheetData
        {
            public Dictionary<string, string> expressions { get; set; } = new();
            public Dictionary<string, int> values { get; set; } = new();
            public int CountColumn { get; set; } = 8;
            public int CountRow { get; set; } = 8;
        }
        private SpreadsheetData fileData = new SpreadsheetData();
        private bool displayValues = false;
        private string fileOpened = "";
        const int MaxCountColumn = 24;
        const int MaxCountRow = 24;
        private bool CircRefTest(string curCell, string Formula)
        {
            List<string> refs = GetCellRefs(Formula);
            if (refs.Count == 0) return false;

            var dependencies = new Dictionary<string, List<string>>();

            foreach (var (cell, formula) in fileData.expressions)
            {
                if (cell == curCell) continue;

                dependencies[cell] = GetCellRefs(formula);
            }

            dependencies[curCell] = refs;
            var visited = new HashSet<string>();

            foreach (var startCell in refs)
            {
                if (CheckCircularReference(curCell, startCell, visited, dependencies))
                {
                    return true;
                }
            }

            return false;
        }
        private bool CheckCircularReference(string targetCell, string curCell, HashSet<string> visitedCells, Dictionary<string, List<string>> dependencies)
        {
            if (curCell == targetCell) return true;

            if (!visitedCells.Add(curCell)) return false;

            if (dependencies.TryGetValue(curCell, out var links))
            {
                foreach (var neighbour in links)
                {
                    if (CheckCircularReference(targetCell, neighbour, visitedCells, dependencies))
                        return true;
                }
            }

            return false;
        }
        private bool InvalidRefTest(string formula)
        {
            List<string> references = GetCellRefs(formula);

            foreach (string refCell in references)
            {
                if (OutOfBoundsCheck(refCell))
                {
                    return true;
                }
            }
            return false;
        }
        private bool OutOfBoundsCheck(string cellRef)
        {
            var match = Regex.Match(cellRef, @"^([A-Z]+)(\d+)$");
            if (!match.Success) return true;

            string colPart = match.Groups[1].Value;
            string rowPart = match.Groups[2].Value;

            int colIndex = GetColumnIndex(colPart);
            int rowIndex = int.Parse(rowPart);
            return colIndex > fileData.CountColumn || rowIndex > fileData.CountRow;
        }
        private List<string> GetCellRefs(string formula)
        {
            if (string.IsNullOrWhiteSpace(formula))
                return new List<string>();

            try
            {
                ICharStream stream = new AntlrInputStream(formula);
                lab1_calcLexer lexer = new CalculatorLexer(stream);
                CommonTokenStream tokens = new CommonTokenStream(lexer);

                tokens.Fill();
                return tokens.GetTokens().Where(t => t.Type == lab1_calcLexer.REFERENCE).Select(t => t.Text).Distinct().ToList();
            }
            catch
            {
                return new List<string>();
            }
        }
        public MainPage()
        {
            InitializeComponent();
            CreateGrid();
        }
        private void CreateGrid()
        {
            AddColumnsAndColumnLabels();
            AddRowsAndCellEntries();
        }
        private void AddColumnsAndColumnLabels()
        {
            for (int col = 0; col < fileData.CountColumn + 1; col++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                if (col > 0)
                {
                    var label = new Label
                    {
                        Text = GetColumnName(col),
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.Center
                    };
                    Grid.SetRow(label, 0);
                    Grid.SetColumn(label, col);
                    grid.Children.Add(label);
                }
            }
        }
        private void AddRowsAndCellEntries()
        {
            if (grid.RowDefinitions.Count == 0)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            for (int row = 0; row < fileData.CountRow; row++)
            {
                grid.RowDefinitions.Add(new RowDefinition());
                var label = new Label
                {
                    Text = (row + 1).ToString(),
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Center
                };
                Grid.SetRow(label, row + 1);
                Grid.SetColumn(label, 0);
                grid.Children.Add(label);

                for (int col = 0; col < fileData.CountColumn; col++)
                {
                    var entry = new Entry
                    {
                        Text = "",
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.Center
                    };
                    entry.Unfocused += Entry_Unfocused;
                    Grid.SetRow(entry, row + 1);
                    Grid.SetColumn(entry, col + 1);
                    grid.Children.Add(entry);
                }
            }
        }
        private string GetColumnName(int colIndex)
        {
            int dividend = colIndex;
            string columnName = string.Empty;
            while (dividend > 0)
            {
                int modulo = (dividend - 1) % 26;
                columnName = Convert.ToChar(65 + modulo) + columnName;
                dividend = (dividend - modulo) / 26;
            }
            return columnName;
        }
        private int GetColumnIndex(string columnName)
        {
            int index = 0;
            for (int i = 0; i < columnName.Length; i++)
            {
                index *= 26;
                index += (columnName[i] - 'A' + 1);
            }
            return index;
        }
        private void Entry_Unfocused(object sender, FocusEventArgs e)
        {
            var entry = (Entry)sender;
            var row = Grid.GetRow(entry) - 1;
            var col = Grid.GetColumn(entry) - 1;
            var cellRef = $"{GetColumnName(col + 1)}{row + 1}";

            var content = entry.Text;
            fileData.expressions[cellRef] = content;
            if (!Title.Contains("*"))Title = Title + "*";
            if (string.IsNullOrWhiteSpace(content))
            {
                fileData.expressions.Remove(cellRef);
                fileData.values.Remove(cellRef);
                return;
            }
        }

        private void ToggleDisplayModeButton_Clicked(object sender, EventArgs e)
        {
            displayValues = !displayValues;
            foreach (var child in grid.Children)
            {
                if (child is Entry entry)
                {
                    int row = Grid.GetRow(entry) - 1;
                    int col = Grid.GetColumn(entry) - 1;
                    var cellRef = $"{GetColumnName(col + 1)}{row + 1}";

                    if (!displayValues)
                    {
                        entry.Text = fileData.values.TryGetValue(cellRef, out int val) ? val.ToString() : "";
                        
                    }
                    else
                    {
                        entry.Text = fileData.expressions.TryGetValue(cellRef, out string expr) ? expr : "";
                    }
                    entry.IsReadOnly = displayValues;
                }
            }
        }
        private void CalculateButton_Clicked(object sender, EventArgs e)
        {
            fileData.values.Clear();
            foreach (var (cellRef, formula) in fileData.expressions)
            {
                if (string.IsNullOrWhiteSpace(formula)) continue;

                if (CircRefTest(cellRef, formula))
                {
                    fileData.values[cellRef] = -2;
                    continue;
                }
                if (InvalidRefTest(formula))
                {
                    fileData.values[cellRef] = -3;
                    continue;
                }
                int result = Calculator.Evaluate(formula, fileData.values);
                fileData.values[cellRef] = result;
            }
        }
        private async void SaveButton_Clicked(object sender, EventArgs e)
        {
            string path = await DisplayPromptAsync("Зберегти файл", "Введіть повний шлях до файлу:", placeholder: @"C:\foldername\worksheet.json");

            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                var saveData = new SpreadsheetData
                {
                    expressions = fileData.expressions,
                    values = fileData.values,
                    CountRow = fileData.CountRow,
                    CountColumn = fileData.CountColumn
                };

                string json = JsonSerializer.Serialize(saveData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                Title = path;
                await DisplayAlertAsync("Операцію виконано", "Таблицю успішно збережено.", "ОК");
            }
            catch (UnauthorizedAccessException)
            {
                await DisplayAlertAsync("Відмова в доступі", "Ви не маєте системних прав запису в зазначену папку.", "ОК");
            }
            catch (DirectoryNotFoundException)
            {
                await DisplayAlertAsync("Неправильний шлях", "Вказаної папки або групи папок не існує.", "ОК");
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Помилка", "Сталась невідома помилка при зберіганні.", "ОК");
            }
        }
        private async void OpenButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                var customFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>{
                {DevicePlatform.WinUI, new[]{ ".json" }}
                    });

                PickOptions options = new PickOptions
                {
                    PickerTitle = "Відкрити файл таблиці",
                    FileTypes = customFileType
                };

                FileResult result = await FilePicker.Default.PickAsync(options);
                Title = result.FullPath;

                if (result != null)
                {
                    using Stream stream = await result.OpenReadAsync();
                    using StreamReader reader = new StreamReader(stream);
                    string jsonContent = await reader.ReadToEndAsync();

                    SpreadsheetData data = JsonSerializer.Deserialize<SpreadsheetData>(jsonContent);

                    if (data != null)
                    {
                        grid.Children.Clear();
                        grid.RowDefinitions.Clear();
                        grid.ColumnDefinitions.Clear();

                        fileData.CountRow = data.CountRow;
                        fileData.CountColumn = data.CountColumn;
                        AddRowsAndCellEntries();

                        fileData.expressions = data.expressions ?? new();
                        fileData.values = data.values ?? new();

                        foreach (var child in grid.Children)
                        {
                            if (child is Entry entry)
                            {
                                int row = Grid.GetRow(entry);
                                int col = Grid.GetColumn(entry);

                                string colName = GetColumnName(col);
                                string cellRef = $"{colName}{row}";

                                if (fileData.expressions.TryGetValue(cellRef, out string formula))
                                {
                                    entry.Text = formula;
                                }
                            }
                        }

                        displayValues = false;
                        CalculateButton_Clicked(null, null);
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Помилка відкриття", $"Не вдалось відкрити файл через таку причину: {ex.Message}", "ОК");
            }
        }
        private async void ExitButton_Clicked(object sender, EventArgs e)
        {
            bool answer = await DisplayAlertAsync("Підтвердження", "Ви дійсно хочете вийти з табличної програми? Переконайтесь, що зберегли таблицю перед виходом", "Так", "Ні");
            if (answer)
            {
                System.Environment.Exit(0);
            }
        }
        private async void HelpButton_Clicked(object sender, EventArgs e)
        {
            await DisplayAlertAsync("Довідка", """
                
                Лабораторна робота 1 студента Юхти Матвія. Варіант 76.
                Коди помилок: -1 = синтаксична помилка, -2 = циклічне посилання, -3 = посилання на неіснуючу комірку.
                Доступні функції: NOT, AND, OR, MMAX, MMIN, >, <, =, inc, dec.
                Функції вводяться без знаку = перед ними.
                AND, OR, <=> стоять МІЖ операндами.

                """, "OK");
        }
        private void DeleteRowButton_Clicked(object sender, EventArgs e)
        {
            if (grid.RowDefinitions.Count > 1)
            {
                int lastRowIndex = grid.RowDefinitions.Count - 1;
                var deletedCells = grid.Children
                                       .Where(child => Grid.GetRow((BindableObject)child) == lastRowIndex)
                                       .ToList();
                foreach (var cell in deletedCells) { grid.Children.Remove(cell); }
                grid.RowDefinitions.RemoveAt(lastRowIndex);
                --fileData.CountRow;

                for (int col = 1; col <= fileData.CountColumn; col++)
                {
                    string colName = GetColumnName(col);
                    string cellRef = $"{colName}{lastRowIndex}";
                    fileData.expressions.Remove(cellRef);
                    fileData.values.Remove(cellRef);
                }
                if (!Title.Contains("*")) Title = Title + "*";
            }
        }
        private void DeleteColumnButton_Clicked(object sender, EventArgs e)
        {
            if (grid.ColumnDefinitions.Count > 1)
            {
                int lastColumnIndex = grid.ColumnDefinitions.Count - 1;
                var deletedCells = grid.Children
                                       .Where(child => Grid.GetColumn((BindableObject)child) == lastColumnIndex)
                                       .ToList();
                foreach (var cell in deletedCells) { grid.Children.Remove(cell); }
                grid.ColumnDefinitions.RemoveAt(lastColumnIndex);
                --fileData.CountColumn;

                string colName = GetColumnName(lastColumnIndex);
                for (int row = 1; row <= fileData.CountRow; row++)
                {
                    string cellRef = $"{colName}{row}";
                    fileData.expressions.Remove(cellRef);
                    fileData.values.Remove(cellRef);
                }
                if (!Title.Contains("*")) Title = Title + "*";
            }
        }
        private void AddRowButton_Clicked(object sender, EventArgs e)
        {
            if (fileData.CountRow < MaxCountRow)
            {
                int newRow = grid.RowDefinitions.Count;
                grid.RowDefinitions.Add(new RowDefinition());
                var label = new Label
                {
                    Text = newRow.ToString(),
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Center
                };
                Grid.SetRow(label, newRow);
                Grid.SetColumn(label, 0);
                grid.Children.Add(label);

                for (int col = 0; col < fileData.CountColumn; col++)
                {
                    var entry = new Entry
                    {
                        Text = "",
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.Center
                    };
                    entry.Unfocused += Entry_Unfocused;
                    Grid.SetRow(entry, newRow);
                    Grid.SetColumn(entry, col + 1);
                    grid.Children.Add(entry);
                }
                ++fileData.CountRow;
                if (!Title.Contains("*")) Title = Title + "*";
            }
        }
        private void AddColumnButton_Clicked(object sender, EventArgs e)
        {
            if (fileData.CountColumn < MaxCountColumn)
            {
                int newColumn = grid.ColumnDefinitions.Count;
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                var label = new Label
                {
                    Text = GetColumnName(newColumn),
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Center
                };
                Grid.SetRow(label, 0);
                Grid.SetColumn(label, newColumn);
                grid.Children.Add(label);

                for (int row = 0; row < fileData.CountRow; row++)
                {
                    var entry = new Entry
                    {
                        Text = "",
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.Center
                    };
                    entry.Unfocused += Entry_Unfocused;
                    Grid.SetRow(entry, row + 1);
                    Grid.SetColumn(entry, newColumn);
                    grid.Children.Add(entry);
                }
                ++fileData.CountColumn;
                if (!Title.Contains("*")) Title = Title + "*";
            }
        }
    }
}