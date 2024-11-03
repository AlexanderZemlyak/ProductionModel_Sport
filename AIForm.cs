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
}
