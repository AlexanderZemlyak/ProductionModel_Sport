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

        private Fact dummyGoal = new Fact("DummyGoal");
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
                        if (rule.RHS != dummyGoal)
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

            //targetListBox1.Items.Add(string.Join(", ", currentFacts.Where(fact => fact.IsGoal)));

            goalFacts.AddRange(currentFacts.Where(fact => fact.IsGoal));
        }
        private void BackwardChaining()
        {
            var targetGoals = allFacts.Where(f => f.IsGoal).ToList();

            foreach (var goal in targetGoals)
            {
                rules.Add(new Production(lhs: new Fact[] { goal }, rhs: dummyGoal));
            }
            // Инициализация
            HashSet<Fact> initialFacts = GetInitialFacts().ToHashSet();
            HashSet<Fact> foundFacts = new HashSet<Fact>(initialFacts);
            Stack<Fact> stack = new Stack<Fact>();
            HashSet<Production> appliedRules = new HashSet<Production>();
            // Группировка правил по факту в правой части (RHS)
            Dictionary<Fact, HashSet<Production>> alternativeRules = rules
                .GroupBy(r => r.RHS)
                .ToDictionary(g => g.Key, g => new HashSet<Production>(g));

            Dictionary<Fact, Production> currentRule = new Dictionary<Fact, Production>();
            List<Snapshot> result = new List<Snapshot>();
            bool noSolution = false;
            HashSet<Production> branch = new HashSet<Production>();

            // Пушим фиктивную вершину в стек, чтобы начать процесс поиска
            stack.Push(dummyGoal);

            // Отслеживаем посещенные факты для предотвращения циклов
            HashSet<Fact> visited = new HashSet<Fact>();
            // Основной цикл поиска
            while (stack.Count > 0)
            {
                Fact currentFact = stack.Pop();
                //if (visited.Contains(currentFact))
                    //continue;
                visited.Add(currentFact);
                // Проверяем, есть ли альтернативные правила для текущего факта
                if (!alternativeRules.ContainsKey(currentFact) && !initialFacts.Contains(currentFact))
                {
                    // Если для currentFact нет правил, ищем альтернативные пути
                    while (stack.Count > 0 && (!alternativeRules.ContainsKey(stack.Peek()) || alternativeRules[stack.Peek()].Count == 0))
                    {
                        var factToRemove = stack.Pop();

                        // Удаляем правило из branch, связанное с данным фактом, если оно есть в currentRule
                        if (currentRule.TryGetValue(factToRemove, out var ruleToRemove))
                        {
                            branch.Remove(ruleToRemove);
                        }
                    }
                    if (stack.Count == 0)
                    {

                        noSolution = true;
                        break;
                    }
                    else
                    {
                        currentFact = stack.Pop();
                        if (alternativeRules.ContainsKey(currentFact))
                        {
                            currentRule[currentFact] = alternativeRules[currentFact].Last();
                            alternativeRules[currentFact].Remove(currentRule[currentFact]);
                        }
                    }
                }

                // Выбираем текущее правило, если оно ещё не было выбрано
                if (!currentRule.ContainsKey(currentFact) && alternativeRules.ContainsKey(currentFact))
                {
                    
                    if (alternativeRules[currentFact].Count > 0)
                    {
                        currentRule[currentFact] = alternativeRules[currentFact].Last();
                        alternativeRules[currentFact].Remove(currentRule[currentFact]);
                    }
                    else
                    {
                        noSolution = true;
                        break;
                    }
                }

                // Получаем продукцию для текущего факта из currentRule
                if (currentRule.TryGetValue(currentFact, out var rule))
                {
                    // Проверяем, что правило не зацикливается и все левые части не найдены
                    if (branch.Contains(rule) && rule.LHS.All(lhs => !foundFacts.Contains(lhs)))
                    {
                        while (stack.Count > 0 && (!alternativeRules.ContainsKey(stack.Peek()) || alternativeRules[stack.Peek()].Count == 0))
                        {
                            var factToRemove = stack.Pop();

                            // Удаляем правило из branch, связанное с данным фактом
                            if (currentRule.TryGetValue(factToRemove, out var ruleToRemove))
                            {
                                branch.Remove(ruleToRemove);
                            }
                        }
                        if (stack.Count > 0)
                        {
                            currentFact = stack.Pop();
                            if (alternativeRules.ContainsKey(currentFact))
                            {
                                currentRule[currentFact] = alternativeRules[currentFact].Last();
                                alternativeRules[currentFact].Remove(currentRule[currentFact]);
                                rule = currentRule[currentFact];
                            }
                        }
                        else
                        {
                            noSolution = true;
                            break;
                        }
                    }

                    // Добавляем правило в ветку и проверяем левые части
                    branch.Add(rule);
                    var notFoundFacts = rule.LHS.Where(lhs => !foundFacts.Contains(lhs)).ToList();
                    if (!foundFacts.Contains(rule.RHS))  // Проверка, добавлен ли RHS уже в foundFacts
                    {
                        // Если правило ещё не применялось, добавляем его
                        if (!appliedRules.Contains(rule))
                        {
                            appliedRules.Add(rule);
                        }
                        if (notFoundFacts.Count == 0)
                        {
                            branch.Remove(rule);
                            foundFacts.Add(rule.RHS);
                            result.Add(new Snapshot(new HashSet<Fact>(foundFacts), rule));
                        }
                        else
                        {
                            stack.Push(currentFact);
                            foreach (var lhs in notFoundFacts)
                            {
                                    stack.Push(lhs);
                                }
                            }
                        }
                    }
                }

            // Если решения нет, возвращаем пустой результат
            if (noSolution)
            {
                outputWindow.AppendText("Не удалось найти путь к целевому факту.\n");
            }
            else
            {
                outputWindow.AppendText("Оптимальная последовательность правил найдена.\n");
                foreach (var snapshot in result)
                {
                    if (snapshot.AppliedRule.RHS != dummyGoal)
                        outputWindow.AppendText(snapshot.AppliedRule.ToString() + "\n");
                }
            }
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
            BackwardChaining();
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
