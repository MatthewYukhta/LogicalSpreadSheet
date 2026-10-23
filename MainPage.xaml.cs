using Antlr4.Runtime;
using lab1_calc;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Compatibility;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Windows.UI.WebUI;
using CalculatorLexer = lab1_calc.lab1_calcLexer;
using CalculatorParser = lab1_calc.lab1_calcParser;
using CalculatorVisitor = lab1_calc.lab1_calcVisitor;
using Grid = Microsoft.Maui.Controls.Grid;
namespace LogicalSpreadSheet
{
    public partial class MainPage : ContentPage
    {
        private Dictionary<string, string> expressions = new();
        private Dictionary<string, int> values = new();
        private bool displayValues = false;
        private bool CircRefTest(string curCell, string Formula)
        {
            List<string> refs = GetCellRefs(Formula);
            if (refs.Count == 0) return false;

            var dependencies = new Dictionary<string, List<string>>();

            foreach (var (cell, formula) in expressions)
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

        private List<string> GetCellRefs(string formula)
        {
            if (string.IsNullOrWhiteSpace(formula))
                return new List<string>();

            try
            {
                ICharStream stream = new AntlrInputStream(formula);
                lab1_calcLexer lexer = new CalculatorLexer(stream);
                CommonTokenStream tokens = new CommonTokenStream(lexer);

                // Pull all IDENTIFIER tokens directly from the token stream
                tokens.Fill();
                return tokens.GetTokens().Where(t => t.Type == lab1_calcLexer.REFERENCE).Select(t => t.Text).Distinct().ToList();
            }
            catch
            {
                return new List<string>();
            }
        }
        const int CountColumn = 20;
        const int CountRow = 50;
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
            // Додати стовпці та підписи для стовпців
            for (int col = 0; col < CountColumn + 1; col++)
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
            // Додати рядки, підписи для рядків та комірки
            for (int row = 0; row < CountRow; row++)
            {
                grid.RowDefinitions.Add(new RowDefinition());
                // Додати підпис для номера рядка
                var label = new Label
                {
                    Text = (row + 1).ToString(),
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Center
                };
                Grid.SetRow(label, row + 1);
                Grid.SetColumn(label, 0);
                grid.Children.Add(label);
                // Додати комірки (Entry) для вмісту
                for (int col = 0; col < CountColumn; col++)
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
        private void Entry_Unfocused(object sender, FocusEventArgs e)
        {
            var entry = (Entry)sender;
            var row = Grid.GetRow(entry) - 1;
            var col = Grid.GetColumn(entry) - 1;
            var cellRef = $"{(char)('A' + col)}{row + 1}";
            var content = entry.Text;

            if (string.IsNullOrWhiteSpace(content))
            {
                expressions.Remove(cellRef);
                values.Remove(cellRef);
                return;
            }

            expressions[cellRef] = content;
        }

        private void ToggleDisplayModeButton_Clicked(object sender, EventArgs e)
        {
            foreach (var child in grid.Children)
            {
                if (child is Entry entry)
                {
                    int row = Grid.GetRow(entry) - 1;
                    int col = Grid.GetColumn(entry) - 1;
                    string cellRef = $"{(char)('A' + col)}{row + 1}";

                    if (!displayValues)
                    {
                        entry.Text = values.TryGetValue(cellRef, out int val) ? val.ToString() : "0";
                        displayValues = true;
                    }
                    else
                    {
                        entry.Text = expressions.TryGetValue(cellRef, out string expr) ? expr : "";
                        displayValues = false;
                    }
                }
            }
        }
        private void CalculateButton_Clicked(object sender, EventArgs e)
        {
            values.Clear();
            foreach (var (cellRef, formula) in expressions)
            {
                if (string.IsNullOrWhiteSpace(formula)) continue;

                if (CircRefTest(cellRef, formula))
                {
                    values[cellRef] = -2;
                    continue;
                }
                try
                {
                    ICharStream stream = new AntlrInputStream(formula);
                    lab1_calcLexer lexer = new CalculatorLexer(stream);
                    CommonTokenStream tokens = new CommonTokenStream(lexer);
                    lab1_calcParser parser = new CalculatorParser(tokens);

                    var visitor = new CalculatorVisitor(values);

                    bool resultBool = visitor.Visit(parser.compileUnit());
                    values[cellRef] = resultBool ? 1 : 0;
                }
                catch (Exception ex)
                {
                    values[cellRef] = -1;
                }
            }
        }
        private void SaveButton_Clicked(object sender, EventArgs e)
        {
            // Обробка кнопки "Зберегти"
        }
        private void ReadButton_Clicked(object sender, EventArgs e)
        {
            // Обробка кнопки "Прочитати"
        }
        private async void ExitButton_Clicked(object sender, EventArgs e)
        {
            bool answer = await DisplayAlertAsync("Підтвердження", "Ви дійсно хочете вийти з табличної програми?", "Так", "Ні");
            if (answer)
            {
                System.Environment.Exit(0);
            }
        }
        private async void HelpButton_Clicked(object sender, EventArgs e)
        {
            await DisplayAlertAsync("Довідка", "Лабораторна робота 1 студента Юхти М. (НЕЗАВЕРШЕНА)\nКоди помилок: -1 = синтаксична помилка, -2 = циклічне посилання.", "OK");
        }
        private void DeleteRowButton_Clicked(object sender, EventArgs e)
        {
            if (grid.RowDefinitions.Count > 1)
            {
                int lastRowIndex = grid.RowDefinitions.Count - 1;
                grid.RowDefinitions.RemoveAt(lastRowIndex);
                grid.Children.RemoveAt(lastRowIndex * (CountColumn + 1)); // Remove label
                for (int col = 0; col < CountColumn; col++)
                {
                    grid.Children.RemoveAt((lastRowIndex * CountColumn) + col + 1); // Remove entry
                }
            }
        }
        private void DeleteColumnButton_Clicked(object sender, EventArgs e)
        {
            if (grid.ColumnDefinitions.Count > 1)
            {
                int lastColumnIndex = grid.ColumnDefinitions.Count - 1;
                grid.ColumnDefinitions.RemoveAt(lastColumnIndex);
                grid.Children.RemoveAt(lastColumnIndex); // Remove label

                for (int row = 0; row < CountRow; row++)
                {
                    grid.Children.RemoveAt(row * (CountColumn + 1) + lastColumnIndex + 1); // Remove entry
                }
            }
        }
        private void AddRowButton_Clicked(object sender, EventArgs e)
        {
            int newRow = grid.RowDefinitions.Count;
            // Add a new row definition
            grid.RowDefinitions.Add(new RowDefinition());
            // Add label for the row number
            var label = new Label
            {
                Text = newRow.ToString(),
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            };
            Grid.SetRow(label, newRow);
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);
            // Add entry cells for the new row
            for (int col = 0; col < CountColumn; col++)
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
        }
        private void AddColumnButton_Clicked(object sender, EventArgs e)
        {
            int newColumn = grid.ColumnDefinitions.Count;
            // Add a new column definition
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            // Add label for the column name
            var label = new Label
            {
                Text = GetColumnName(newColumn),
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            };
            Grid.SetRow(label, 0);
            Grid.SetColumn(label, newColumn);
            grid.Children.Add(label);
            // Add entry cells for the new column
            for (int row = 0; row < CountRow; row++)
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
        }
    }
}