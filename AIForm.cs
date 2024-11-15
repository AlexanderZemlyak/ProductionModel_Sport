using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ProductionModel
{
    public partial class AIForm : Form
    {

        private List<Fact> allFacts = new List<Fact>();

        private Node[] allRealNodes;

        private List<Production> rules = new List<Production>();

        private ComboBox[] boxes;

        private Dictionary<ComboBox, Fact[]> comboFacts = new Dictionary<ComboBox, Fact[]>();

        private const string sectionDivider = "-----";

        private const string goalMark = "S";

        private Node dummy;

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

            BuildTreeFromAllRules();

            // TreePrinter printer = new TreePrinter(outputWindow);
            // printer.PrintTree(dummy);
        }

        private void BuildTreeFromAllRules()
        {
            allRealNodes = allFacts.Select(fact => new Node(NodeType.OR, false, fact, 0, null)).ToArray();

            dummy = new Node(NodeType.OR, true, null, 0, allRealNodes.Where(node => node.Fact.IsGoal).ToArray());

            Queue<Node> q = new Queue<Node>();
            foreach (var node in dummy.Children)
            {
                node.Depth = 1;
                q.Enqueue(node);
            }

            HashSet<Production> currentRules = rules.ToHashSet();

            while (q.Count != 0)
            {
                Node current = q.Dequeue();

                var neededRules = currentRules.Where(rule => rule.RHS == current.Fact).ToArray();

                int count = neededRules.Length;

                if (count == 0)
                    continue;

                List<Node> children = new List<Node>();

                foreach (var rule in neededRules)
                {
                    if (count > 1)
                    {
                        if (rule.LHS.Length > 1)
                        {
                            Node controlNode = new Node(NodeType.AND, true, null, 0,
                                rule.LHS.Select(fact => allRealNodes[fact.Index]).ToArray());

                            children.Add(controlNode);

                            foreach (var node in controlNode.Children)
                            {
                                q.Enqueue(node);
                            }
                        }
                        else
                        {
                            children.Add(allRealNodes[rule.LHS[0].Index]);
                            q.Enqueue(allRealNodes[rule.LHS[0].Index]);
                        }
                    }
                    else
                    {
                        if (rule.LHS.Length > 1)
                        {
                            current.Type = NodeType.AND;
                            
                            foreach (var node in rule.LHS.Select(fact => allRealNodes[fact.Index]))
                            {
                                children.Add(node);
                                q.Enqueue(node);
                            }
                        }
                        else
                        {
                            children.Add(allRealNodes[rule.LHS[0].Index]);
                            q.Enqueue(allRealNodes[rule.LHS[0].Index]);
                        } 
                    }

                    currentRules.Remove(rule);
                }

                current.Children = children.ToArray();

                foreach (var child in current.Children)
                {
                    if (child.Depth == 0)
                    {
                        child.Depth = current.Depth + 1;
                    }
                    else if (child.Type == NodeType.AND)
                    {
                        foreach (var nextChild in child.Children)
                        {
                            if (nextChild.Depth == 0)
                                nextChild.Depth = child.Depth + 1;
                        }
                    }
                }
            }
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

                int factIndex = 0;

                for (int i = 0; i < boxes.Length;)
                {
                    int index;
                    if ((index = line.IndexOf('/')) != -1)
                    {
                        line = line.Substring(0, index + 1);
                    }

                    Fact fact = new Fact(string.Join(" ", line.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).Skip(1)), factIndex);

                    factsForCombobox.Add(fact);
                    allFacts.Add(fact);

                    factIndex++;

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

                    Fact fact = new Fact(string.Join(" ", factStrings), factIndex, isGoal);

                    allFacts.Add(fact);

                    factIndex++;

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

            if (currentFacts.FirstOrDefault(fact => fact.IsGoal) == null)
            {
                outputWindow.AppendText("Невозможно вывести виды спорта\n");
            }
            else
            {
                outputWindow.AppendText("\n" + "Полученные виды спорта: ");

                outputWindow.AppendText(string.Join(", ", currentFacts.Where(fact => fact.IsGoal)));
            }
            
        }

        private void BackwardChaining()
        {
            HashSet<Node> initialNodes = GetInitialFacts().Select(fact => allRealNodes[fact.Index]).ToHashSet();

            Queue<Node> q = new Queue<Node>();

            HashSet<Node> closed = new HashSet<Node>();

            HashSet<Node> unsolvable = new HashSet<Node>();

            HashSet<Node> solvable = new HashSet<Node>();

            Dictionary<Node, List<Node>> parents = new Dictionary<Node, List<Node>>();

            q.Enqueue(dummy);

            while (q.Count != 0)
            {
                Node current = q.Dequeue();

                closed.Add(current);

                if (current.Children != null)
                {
                    bool foundInitial = false;

                    foreach (var child in current.Children.OrderBy(child => child.Depth))
                    {
                        if (!parents.ContainsKey(child))
                            parents[child] = new List<Node>();

                        parents[child].Add(current);

                        // цикл
                        if (!closed.Contains(child))
                            q.Enqueue(child);

                        // цикл
                        if (solvable.Contains(child))
                        {
                            if (MarkingSolvable(current, solvable, parents))
                            {
                                // успех - находим нужные правила и выводим
                                PrintSolvingSubtree(initialNodes, parents);

                                return;
                            }
                            else
                            {
                                // удалить лишние вершины с разрешимыми родителями
                            }
                        }
                        // цикл
                        else if (unsolvable.Contains(child))
                        {
                            if (!solvable.Contains(child) && MarkingUnsolvable(child, unsolvable, parents))
                            {
                                outputWindow.AppendText("Невозможно ничего вывести\n");
                                return;
                            }
                            else
                            {
                                // удалить лишние вершины с неразрешимыми родителями
                            }
                        }

                        if (initialNodes.Contains(child))
                        {
                            foundInitial = true;
                            solvable.Add(child);
                        }
                    }

                    if (foundInitial)
                    {
                        // разметка разрешимых
                        if (MarkingSolvable(current, solvable, parents))
                        {
                            // успех - находим нужные правила и выводим
                            PrintSolvingSubtree(initialNodes, parents);

                            return;
                        }
                        else
                        {
                            // удалить лишние вершины с разрешимыми родителями
                        }
                    }
                }
                else
                {
                    // произойдет в MarkingUnsolvable
                    // unsolvable.Add(current);

                    // разметка неразрешимых
                    if (!solvable.Contains(current) && MarkingUnsolvable(current, unsolvable, parents))
                    {
                        outputWindow.AppendText("Невозможно ничего вывести\n");
                        return;
                    }
                    else
                    {
                        // удалить лишние вершины с неразрешимыми родителями
                    }
                }

            }

            outputWindow.AppendText("Невозможно определить выводимость\n");
        }

        private void PrintSolvingSubtree(HashSet<Node> initialNodes, Dictionary<Node, List<Node>> parents)
        {
            Queue<Node> q = new Queue<Node>();

            HashSet<Node> obtainedFacts = new HashSet<Node>();

            // здесь неоптимально, потому что могут быть parents, которые заведут нас вникуда для каких-то initialNodes
            foreach (Node node in initialNodes)
            {
                q.Enqueue(node);
                obtainedFacts.Add(node);
            }

            while (q.Count > 0)
            {
                Node node = q.Dequeue();

                if (parents.ContainsKey(node))
                {
                    foreach (Node parent in parents[node].OrderByDescending(x => x.Depth))
                    {
                        if (parent == dummy)
                            return;
                            
                        if (obtainedFacts.Contains(parent))
                            continue;

                        if (parent.Type == NodeType.OR)
                        {
                            outputWindow.AppendText(new Production(new Fact[1] { node.Fact }, parent.Fact) + "\n");

                            obtainedFacts.Add(parent);

                            q.Enqueue(parent);
                        }
                        else
                        {
                            if (parent.Children.All(x => obtainedFacts.Contains(x)))
                            {
                                if (parent.IsControl)
                                {
                                    Node grandparent = parents[parent][0];

                                    outputWindow.AppendText(new Production(parent.Children.Select(child => child.Fact).ToArray(), grandparent.Fact) + "\n");

                                    obtainedFacts.Add(grandparent);

                                    obtainedFacts.Add(parent);

                                    q.Enqueue(grandparent);
                                }
                                else
                                {
                                    outputWindow.AppendText(new Production(parent.Children.Select(child => child.Fact).ToArray(), parent.Fact) + "\n");

                                    obtainedFacts.Add(parent);

                                    q.Enqueue(parent);
                                }

                                
                            }
                        }
                    }
                }
            }


        }

        private bool MarkingSolvable(Node current, HashSet<Node> solvable, Dictionary<Node, List<Node>> parents)
        {

            if (current == dummy)
            {
                solvable.Add(dummy);
                return true;
            }

            if (current.Type == NodeType.OR)
            {
                solvable.Add(current);
            }
            else
            {
                bool allChildrenSolvable = current.Children.All(child => solvable.Contains(child));

                if (allChildrenSolvable)
                {
                    solvable.Add(current);
                }
                else
                {
                    return false;
                }
            }
            
            foreach (Node parent in parents[current])
            {
                if (solvable.Contains(parent))
                    continue;

                if (MarkingSolvable(parent, solvable, parents))
                    return true;
            }

            return false;
        }

        private bool MarkingUnsolvable(Node current, HashSet<Node> unsolvable, Dictionary<Node, List<Node>> parents)
        {
            
            if (current == dummy)
            {
                bool allChildrenUnsolvable = current.Children.All(child => unsolvable.Contains(child));

                if (allChildrenUnsolvable)
                {
                    
                    return true;
                } 
                else
                {
                    return false;
                }
                    
            }

            if (current.Children != null && current.Type == NodeType.OR)
            {
                bool allChildrenUnsolvable = current.Children.All(child => unsolvable.Contains(child));

                if (allChildrenUnsolvable)
                {
                    unsolvable.Add(current);
                }
                else
                {
                    return false;
                }
            }
            else
            {
                unsolvable.Add(current);
            }

            foreach (Node parent in parents[current])
            {
                if (unsolvable.Contains(parent))
                    continue;

                if (MarkingUnsolvable(parent, unsolvable, parents))
                    return true;
            }

            return false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            outputWindow.Clear();

            ForwardChaining();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                ParseDatabase();

                BuildTreeFromAllRules();

                MessageBox.Show("Факты и правила обновлены. Дерево успешно построено.", "Уведомление", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception)
            {
                MessageBox.Show("Не удалось загрузить данные из файла!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            outputWindow.Clear();

            BackwardChaining();
        }
    }

    public class TreePrinter
    {
        private RichTextBox outputWindow;

        // private HashSet<Node> visitedNodes;

        public TreePrinter(RichTextBox outputWindow)
        {
            this.outputWindow = outputWindow;
        }

        public void PrintTree(Node root)
        {
            outputWindow.Clear();
            // visitedNodes = new HashSet<Node>();
            PrintNode(root, 0);
        }

        private void PrintNode(Node node, int level)
        {
            if (node == null || level > 10)
                return;

            outputWindow.AppendText(new string(' ', level * 2) + (node.Fact?.Index.ToString() ?? "c") + "\n");

            if (node.Children == null)
                return;

            foreach (var child in node.Children)
            {
                PrintNode(child, level + 1);
            }
        }
    }

    public enum NodeType { OR, AND }

    public class Node
    {
        public NodeType Type { get; set; }

        public bool IsControl { get; set; }

        public Fact Fact { get; set; } 

        public Node[] Children { get; set; }

        // public List<Node> Parents { get; set; }

        public int Depth { get; set; }

        public Node(NodeType type, bool isControl, Fact fact, int depth, Node[] children/*, List<Node> parents = null*/)
        {
            Type = type;
            IsControl = isControl;
            
            if (isControl)
                Fact = null;
            else
                Fact = fact;

            Depth = depth;

            Children = children;
            
            /*if (parents == null)
                Parents = new List<Node>();
            else
                Parents = parents;*/
        }
    }

    public class Fact
    {
        public string Description { get; private set; }

        public bool IsGoal { get; private set; }

        public int Index { get; private set; }

        public Fact(string description, int index, bool isGoal = false)
        {
            Description = description;
            Index = index;
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
