using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using JetBrains.Annotations;
using UniLinq;

namespace CodeWriter.ExpressionParser
{
    public class MinEventEvaluator
    {
        private class SimpleTreeNode
        {
            private static int nodeIdCounter = 0;
            private int NodeID { get; } = System.Threading.Interlocked.Increment(ref nodeIdCounter);
            public string LocalID => $"[node_{NodeID}]";

            public string Expr { get; }
            public string ExprWithID { get; private set; }
            public List<SimpleTreeNode> Children { get; } = new();
            public SimpleTreeNode Parent { get; private set; }
            public int Depth { get; private set; }
            public int Index { get; private set; }
            public int MaxChildDepth { get; private set; }

            public VariableInfo parsedInfo;
            private SimpleTreeNode(string expr, SimpleTreeNode parent)
            {
                ExprWithID = Expr = expr;
                ParentTo(parent);
            }

            public void ParentTo(SimpleTreeNode parent)
            {
                if (Parent != null)
                {
                    Parent.Children.RemoveAt(Index);
                    Parent.UpdateMaxChildDepth();
                    Parent = null;
                }
                if (parent != null)
                {
                    Depth = parent.Depth + 1;
                    MaxChildDepth = Depth + MaxChildDepth;
                    parent.Children.Add(this);
                    Parent = parent;
                    UpdateMaxChildDepth();
                }
                else
                {
                    Depth = 0;
                }
                UpdateChildDepth();
            }

            private void UpdateChildDepth()
            {
                foreach (var child in Children)
                {
                    child.Depth = Depth + 1;
                    child.UpdateChildDepth();
                }
            }
            
            private void UpdateMaxChildDepth()
            {
                var currentNode = Parent;
                while (currentNode != null)
                {
                    currentNode.MaxChildDepth = currentNode.Children.Max(x => x.MaxChildDepth);
                    currentNode.Children.Sort((a, b) => b.MaxChildDepth.CompareTo(a.MaxChildDepth));
                    for (int i = 0; i < currentNode.Children.Count; i++)
                    {
                        currentNode.Children[i].Index = i;
                    }
                    currentNode = currentNode.Parent;
                }
            }

            public IEnumerable<SimpleTreeNode> YieldLeaves()
            {
                if (Children.Count == 0)
                {
                    if (Parent != null)
                        yield return this;
                }
                else
                {
                    foreach (var child in Children)
                    {
                        foreach (var leaf in child.YieldLeaves())
                        {
                            yield return leaf;
                        }
                    }
                }
            }

            public IEnumerable<SimpleTreeNode> YieldBranches(bool isRoot)
            {
                if (Children.Count > 0)
                {
                    foreach (var child in Children)
                    {
                        foreach (var branch in child.YieldBranches(false))
                        {
                            yield return branch;
                        }
                    }
                    if (!isRoot)
                    {
                        yield return this;
                    }
                }
            }

            public static bool TryParse(string expression, out SimpleTreeNode root)
            {
                root = new SimpleTreeNode(expression, null);
                if (!string.IsNullOrEmpty(expression))
                {
                    return ParseChildren(root, expression);
                }

                return false;
            }

            private static bool ParseChildren(SimpleTreeNode parent, string content)
            {
                int depth = 0;
                int start = -1;
                bool isValid = true;
                var replacements = new List<(int start, int len, SimpleTreeNode child)>();
                for (int i = 0; i < content.Length; i++)
                {
                    char c = content[i];
                    if (c == '[')
                    {
                        if (depth == 0)
                            start = i;
                        depth++;
                    }
                    else if (c == ']')
                    {
                        if (depth == 0)
                        {
                            isValid = false;
                            break;
                        }
                        depth--;
                        if (depth == 0)
                        {
                            string full = content.Substring(start, i - start + 1);
                            string inner = content.Substring(start + 1, i - start - 1);
                            var child = new SimpleTreeNode(full, parent);
                            replacements.Add((start, i - start + 1, child));
                            if (!ParseChildren(child, inner))
                            {
                                isValid = false;
                                break;
                            }
                        }
                    }
                }

                if (isValid)
                {
                    var sb = new StringBuilder(content);
                    for (int k = replacements.Count - 1; k >= 0; k--)
                    {
                        sb.Remove(replacements[k].start, replacements[k].len);
                        sb.Insert(replacements[k].start, replacements[k].child.LocalID);
                    }

                    if (parent.Depth > 0)
                    {
                        sb.Insert(0, '[');
                        sb.Append(']');
                    }
                    parent.ExprWithID = sb.ToString();
                }

                return isValid;
            }
        }

        [UsedImplicitly] private readonly string expr;
        private readonly Expression<float> compiledExpr; 
        private readonly VariableInfo[] variableInfos;
        private readonly Dictionary<string, VariableInfo> uniqueInfos;

        private MinEventEvaluator(string expr, Expression<float> compiledExpr, VariableInfo[] variableInfos, Dictionary<string, VariableInfo> uniqueInfos)
        {
            this.expr = expr;
            this.variableInfos = variableInfos;
            this.compiledExpr = compiledExpr;
            this.uniqueInfos = uniqueInfos;
        }

        public bool IsValid(MinEventParams _params, EntityAlive _target, int _targetIndex)
        {
            if (compiledExpr == null)
                return false;

            foreach (var info in variableInfos)
            {
                if (info.ValidationRequired && !info.IsValid(_params, _target, _targetIndex))
                    return false;
            }
            
            return true;
        }

        public float Evaluate(MinEventParams _params, EntityAlive _target, int _targetIndex, int _targetCount)
        {
            if (compiledExpr == null)
            {
                return 0;
            }
            var prevOther = _params.Other;
            _params.Other = _target;
            foreach (var info in variableInfos)
            {
                info.Evaluate(_params, _target, _targetIndex, _targetCount);
            }
            _params.Other = prevOther;
            float res = compiledExpr.Invoke();
            return res;
        }

        public void ParseAdditionalAttributes(XElement elem)
        {
            if (variableInfos is not { Length: > 0 } || elem.Name.LocalName != "var")
                return;
            
            string name = elem.GetAttribute("name")?.Replace(" ", "").ToLower();
            if (string.IsNullOrEmpty(name))
                return;

            if (!name.StartsWith("[") && !name.EndsWith("]"))
                name = $"[{name}]";
            
            if (!uniqueInfos.TryGetValue(name, out var info))
            {
                Log.Warning($"Variable {name} not found, maybe typo or variable is not unique");
                return;
            }

            // Log.Out($"Parsing additional attributes for {name} variable index {info.VarIndex}");
            foreach (var attr in elem.Attributes())
            {
                info.ParseAdditionalAttributes(attr);
            }
        }

        [UsedImplicitly] private static Dictionary<string, SimpleTreeNode> aliasNodesDebug;
        [UsedImplicitly] private static SimpleTreeNode rootNodeDebug;
        [UsedImplicitly] private static MinEventEvaluator evaluatorDebug;
        public static bool TryParse(string expr, out MinEventEvaluator evaluator, Func<string, VariableInfo> variableParser = null)
        {
            evaluator = null;
            expr = expr.Replace(" ", "");
            var exprAlias = new DictionaryKeyValueList<string, SimpleTreeNode>
            {
                dict = new Dictionary<string, SimpleTreeNode>(StringComparer.OrdinalIgnoreCase)
            };
            aliasNodesDebug = exprAlias.dict;
            while (expr.StartsWith("$"))
            {
                int index = expr.IndexOf(';');
                if (index < 0)
                {
                    evaluator = null;
                    return false;
                }
                string define = expr[1..index];
                string[] parts = define.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2 || !SimpleTreeNode.TryParse(parts[1], out var node))
                {
                    return false;
                }

                var key = $"[{'{'}{parts[0]}{'}'}]";
                if (exprAlias.dict.TryGetValue(key, out var existingNode))
                {
                    Log.Error($"Duplicate alias {key}: existing expr {existingNode.Expr} new expr {node.Expr}");
                    return false;
                }

                exprAlias.Add(key, node);
                expr = expr[(index + 1)..];
            }

            if (!SimpleTreeNode.TryParse(expr, out var root))
            {
                Log.Error($"Failed to build tree for expr {expr}");
                return false;
            }
            rootNodeDebug = root;
            
            var context = new ExpressionContext<float>();
            List<VariableInfo> variableInfos = new List<VariableInfo>();
            Dictionary<string, int> uniqueInfos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < exprAlias.dict.Count; i++)
            {
                var alias = exprAlias.keyList[i];
                var node = exprAlias.valueList[i];
                if (!TryParseNode(node, alias))
                {
                    Log.Error($"Failed to parse alias {alias} expr {node.Expr}");
                    return false;
                }
            }

            if (!TryParseNode(root, null))
            {
                Log.Error($"Failed to parse root expr {root.Expr}");
                return false;
            }

            expr = root.ExprWithID;
            foreach (var child in root.Children)
            {
                var varName = $"x{child.parsedInfo.VarIndex}";
                var varInfo = child.parsedInfo;
                if (context.GetVariable(varName, true) == null)
                    context.RegisterVariable(varName, () => varInfo.LastEvalValue);
                expr = expr.Replace(child.LocalID, varName);
            }
            
            Log.Out($"Compiling expr {expr}...");
            evaluator = new MinEventEvaluator(expr, FloatExpressionParser.Instance.Compile(expr, context, true),
                variableInfos.ToArray(), uniqueInfos.ToDictionary(p => p.Key, p => variableInfos[p.Value], StringComparer.OrdinalIgnoreCase));
            evaluatorDebug = evaluator;
            return true;

            bool TryGetUniqueInfo(string name, out VariableInfo info)
            {
                if (uniqueInfos.TryGetValue(name, out var index))
                {
                    info = variableInfos[index];
                    return true;
                }

                info = null;
                return false;
            }

            bool TryParseVariable(string varStr, Dictionary<string, VariableInfo> children, string alias, bool forceUnique, out VariableInfo info)
            {
                if (varStr.StartsWith("[{") && varStr.EndsWith("}]"))
                {
                    return TryGetUniqueInfo(varStr, out info);
                }
                
                if (!VariableInfo.TryParse(varStr, out info, children, alias, variableParser))
                {
                    return false;
                }

                bool isUnique = info.IsUnique || forceUnique;
                if (!isUnique || !uniqueInfos.TryGetValue(info.VarName, out var curIndex))
                {
                    curIndex = variableInfos.Count;
                    info.VarIndex = curIndex;
                    variableInfos.Add(info);
                    if (isUnique)
                    {
                        uniqueInfos.Add(info.VarName, curIndex);
                    }
                }

                return true;
            }

            bool TryParseNode(SimpleTreeNode node, string alias)
            {
                foreach (var leaf in node.YieldLeaves())
                {
                    if (!TryParseVariable(leaf.Expr, null, null, false, out leaf.parsedInfo))
                    {
                        Log.Error($"Failed to parse leaf expr {leaf.Expr}");
                        return false;
                    }
                }

                foreach (var branch in node.YieldBranches(true))
                {
                    //use original expr as alias for name matching
                    if (!TryParseVariable(branch.ExprWithID, branch.Children.ToDictionary(x => x.LocalID, x => x.parsedInfo, StringComparer.OrdinalIgnoreCase), branch.Expr, false, out branch.parsedInfo))
                    {
                        Log.Error($"Failed to parse branch expr {branch.Expr}");
                        return false;
                    }
                }

                // root node is only parsed for alias variable
                return alias == null || TryParseVariable(node.ExprWithID, node.Children.ToDictionary(x => x.LocalID, x => x.parsedInfo, StringComparer.OrdinalIgnoreCase), alias, true, out node.parsedInfo);
            }
        }
    }
}