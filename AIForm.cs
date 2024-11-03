using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ProductionModel
{
    public partial class AIForm : Form
    {

        private List<Fact> allFacts = new List<Fact>();

        private List<Production> rules = new List<Production>();

        private ComboBox[] boxes;

        private Dictionary<ComboBox, Fact[]> comboFacts = new Dictionary<ComboBox, Fact[]>();

        private const string sectionDivider = "-----";

        private const string goalMark = "S";

        private Fact goalFact;

        private List<Fact> goalFacts = new List<Fact>();

        public AIForm()
        {
            InitializeComponent();

            boxes = new ComboBox[]
            {
                comboBoxWeight,
                comboBoxAge,
                comboBoxBack,
                comboBoxHeartDisease,
                comboBoxStamina,
                comboBoxHeight,
                comboBoxLifestyle,
                comboBoxTeamOrNot,
                comboBoxGoal,
                comboBoxFlex,
                comboBoxKnee,
                comboBoxPuchkin,
                comboBoxExtreme
            };
            
            ParseDatabase();

        }

        private void ParseDatabase()
        {
            allFacts = new List<Fact>();

            rules = new List<Production>();

            comboFacts = new Dictionary<ComboBox, Fact[]>();

            using (var reader = new StreamReader(Path.Combine(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory), @"..\..\", "database.txt")))
            {
                // начальные факты
                string line = reader.ReadLine();

                List<Fact> factsForCombobox = new List<Fact>();

                for (int i = 0; i < boxes.Length;)
                {
                    int index;
                    if ((index = line.IndexOf('/')) != -1)
                    {
                        line = line.Substring(0, index + 1);
                    }

                    Fact fact = new Fact(string.Join(" ", line.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).Skip(1)));

                    factsForCombobox.Add(fact);
                    allFacts.Add(fact);

                    line = reader.ReadLine();

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        comboFacts[boxes[i]] = factsForCombobox.ToArray();
                        i++;
                        factsForCombobox.Clear();

                        while (string.IsNullOrWhiteSpace(line))
                        {
                            line = reader.ReadLine();
                        }
                    }
                }

                Debug.Assert(line == sectionDivider);

                line = reader.ReadLine();

                // остальные факты
                while (string.IsNullOrWhiteSpace(line))
                {
                    line = reader.ReadLine();
                }

                while (line != sectionDivider)
                {
                    int index;
                    if ((index = line.IndexOf('/')) != -1)
                    {
                        line = line.Substring(0, index + 1);
                    }

                    var factStrings = line.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).Skip(1);

                    bool isGoal = false;

                    if (factStrings.Last() == goalMark)
                    {
                        factStrings = factStrings.Take(factStrings.Count() - 1);
                        isGoal = true;
                    }

                    Fact fact = new Fact(string.Join(" ", factStrings), isGoal);

                    allFacts.Add(fact);

                    line = reader.ReadLine();

                    while (string.IsNullOrWhiteSpace(line))
                    {
                        line = reader.ReadLine();
                    }
                }

                line = reader.ReadLine();

                // продукции
                while (string.IsNullOrWhiteSpace(line))
                {
                    line = reader.ReadLine();
                }

                while (line != null)
                {
                    int index;
                    if ((index = line.IndexOf('/')) != -1)
                    {
                        line = line.Substring(0, index);
                    }

                    var indices = line.Split(new char[] { ' ', ',', '-', '>' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Skip(1)
                                      .Select(stringIndex => int.Parse(stringIndex))
                                      .ToArray();

                    Production rule = new Production(lhs: indices.Take(indices.Length - 1)
                                                            .Select(_index => allFacts[_index])
                                                            .ToArray(),

                                                     rhs: allFacts[indices.Last()]);

                    rules.Add(rule);

                    line = reader.ReadLine();

                    while (string.IsNullOrWhiteSpace(line))
                    {
                        if (line == null)
                            break;

                        line = reader.ReadLine();
                    }
                }
            }
            goalFacts = allFacts.Where(f => f.IsGoal).ToList();
        }

        private Fact[] GetInitialFacts()
        {
            return boxes.Select(box => box.SelectedIndex != -1 ? comboFacts[box][box.SelectedIndex] : null).Where(fact => fact != null).ToArray();
        }

        private void ForwardChaining()
        {
            HashSet<Production> appliedRules = new HashSet<Production>();

            HashSet<Fact> currentFacts = GetInitialFacts().ToHashSet();

            while (appliedRules.Count < rules.Count)
            {
                bool appliedSomething = false;

                foreach (var rule in rules)
                {
                    if (appliedRules.Contains(rule))
                        continue;

                    if (rule.LHS.All(fact => currentFacts.Contains(fact)))
                    {
                        currentFacts.Add(rule.RHS);

                        outputWindow.AppendText(rule.ToString() + "\n");

                        appliedRules.Add(rule);

                        appliedSomething = true;
                    }
                }
                
                if (!appliedSomething)
                    break;
            }

            outputWindow.AppendText("\n" + "Полученные виды спорта: ");

            outputWindow.AppendText(string.Join(", ", currentFacts.Where(fact => fact.IsGoal)));

            targetListBox1.Items.Add(string.Join(", ", currentFacts.Where(fact => fact.IsGoal)));

            goalFacts.AddRange(currentFacts.Where(fact => fact.IsGoal));
        }
        void BackwardChaining(Fact targetGoal)
        {
            // Фиктивная вершина, объединяющая все целевые факты
            Fact dummyGoal = new Fact("DummyGoal");
            rules.Add(new Production(lhs: new List<Fact>{targetGoal}.ToArray(), rhs:(dummyGoal))); // Фиктивное правило 

            // Стек для хранения фактов, которые необходимо проверить
            Stack<Fact> stack = new Stack<Fact>();
            Dictionary<Fact, Production> factToRule = new Dictionary<Fact, Production>(); // Связываем факты с правилами
            HashSet<Fact> foundFacts = new HashSet<Fact>(allFacts); // Хранит все уже найденные факты

            // Пушим фиктивную вершину в стек, чтобы начать процесс поиска
            stack.Push(dummyGoal);

            while (stack.Count > 0)
            {
                Fact currentFact = stack.Pop();

                // Если факт уже найден, пропускаем его
                //if (foundFacts.Contains(currentFact))
                    //continue;

                // Ищем все правила, ведущие к текущему факту
                var applicableRules = rules.Where(r => r.RHS == currentFact).ToList();
                if (applicableRules.Count == 0)
                {
                    outputWindow.AppendText($"Не удалось найти правила для факта: {currentFact.Description}");
                    return;
                }

                bool factProven = false;

                foreach (var rule in applicableRules)
                {
                    // Проверяем, найдены ли все левые части для текущего правила
                    bool allCausesFound = rule.LHS.All(lhs => foundFacts.Contains(lhs));

                    if (allCausesFound)
                    {
                        // Если все левые части найдены, добавляем текущий факт как доказанный
                        foundFacts.Add(rule.RHS);
                        factToRule[rule.RHS] = rule; // Сохраняем правило для обратного построения
                        factProven = true;
                        break;
                    }
                    else
                    {
                        // Если левые части не найдены, добавляем текущий факт и его левые части в стек
                        stack.Push(currentFact);
                        foreach (var lhs in rule.LHS)
                        {
                            if (!foundFacts.Contains(lhs))
                                stack.Push(lhs);
                        }
                        break;
                    }
                }

                if (!factProven)
                {
                    outputWindow.AppendText($"Не удалось доказать факт: {currentFact.Description}");
                    return;
                }
            }

            // Выводим результат, проходя от целевого факта к начальному
            outputWindow.AppendText("Оптимальная последовательность для достижения цели:");
            PrintSolution(dummyGoal, factToRule);
        }

        // Вспомогательная функция для рекурсивного вывода цепочки правил и фактов
        void PrintSolution(Fact fact, Dictionary<Fact, Production> factToRule)
        {
            if (allFacts.Contains(fact))
            {
                outputWindow.AppendText($"Начальный факт: {fact.Description}");
                return;
            }

            if (factToRule.ContainsKey(fact))
            {
                var rule = factToRule[fact];
                outputWindow.AppendText($"Для достижения {fact.Description} использовано правило:");
                outputWindow.AppendText($"LHS: {string.Join(", ", rule.LHS.Select(f => f.Description))} => RHS: {rule.RHS.Description}");

                foreach (var lhs in rule.LHS)
                {
                    PrintSolution(lhs, factToRule);
                }
            }
            else
            {
                outputWindow.AppendText($"Факт {fact.Description} не может быть доказан.");
            }
        }


        // Метод для запуска алгоритма на основе выбора пользователя
        public void StartBackwardChaining()
        {
            // Выполняем прямой вывод, чтобы определить достижимые целевые факты
            ForwardChaining();
            //List<Fact> reachableGoals = goalFacts;

            // Отображаем список достижимых целевых фактов для выбора
            targetListBox1.Items.Clear();
            foreach (var goal in goalFacts)
            {
                targetListBox1.Items.Add(goal.ToString());
            }

            var selectedGoal = goalFact;
            if (selectedGoal == null)
            {
                outputWindow.AppendText("Целевой факт не выбран.\n");
                return;
            }

            
            outputWindow.AppendText("\nЗапуск обратного вывода для выбранного вида спорта: " + selectedGoal.Description + "\n");
            BackwardChaining(selectedGoal);
        }
        private void button1_Click(object sender, EventArgs e)
        {
            outputWindow.Clear();

            ForwardChaining();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            ParseDatabase();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            outputWindow.Clear();
            //BackwardChaining(goalFact);
            StartBackwardChaining();
        }

        private void targetListBox1_MouseClick(object sender, MouseEventArgs e)
        {
            goalFact = new Fact(description:targetListBox1.Items[targetListBox1.SelectedIndex].ToString(), isGoal: true);
        }
    }

    public class Fact
    {
        public string Description { get; private set; }

        public bool IsGoal { get; private set; }

        public Fact(string description, bool isGoal = false)
        {
            Description = description;
            IsGoal = isGoal;
        }

        public override string ToString()
        {
            return Description;
        }
        public override bool Equals(object obj)
        {
            return obj is Fact fact && Description == fact.Description;
        }

        public override int GetHashCode()
        {
            return (Description != null ? Description.GetHashCode() : 0);
        }
    }

    public class Production
    {
        public Fact[] LHS { get; private set; }

        public Fact RHS { get; private set; }

        public Production(Fact[] lhs, Fact rhs)
        {
            LHS = lhs;
            RHS = rhs;
        }

        public override string ToString()
        {
            return string.Join(", ", LHS.Select(fact => fact.ToString())) + " -> " + RHS;
        }
    }

    public class Snapshot
    {
        public HashSet<Fact> Facts { get; }
        public Production AppliedRule { get; }

        public Snapshot(HashSet<Fact> facts, Production appliedRule)
        {
            Facts = new HashSet<Fact>(facts);
            AppliedRule = appliedRule;
        }

        public override string ToString()
        {
            return $"Facts: {string.Join(", ", Facts.Select(f => f.Description))}; Applied Rule: {AppliedRule}";
        }
    }
}
