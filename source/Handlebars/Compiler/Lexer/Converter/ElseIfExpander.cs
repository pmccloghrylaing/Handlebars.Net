using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace HandlebarsDotNet.Compiler
{
    internal class ElseIfExpander : TokenConverter
    {
        public static IEnumerable<object> Expand(
            IEnumerable<object> tokens,
            HandlebarsConfiguration configuration)
        {
            return new ElseIfExpander(configuration).ConvertTokens(tokens).ToList();
        }

        private readonly HandlebarsConfiguration _configuration;

        private ElseIfExpander(HandlebarsConfiguration configuration)
        {
            _configuration = configuration;
        }

        public override IEnumerable<object> ConvertTokens(IEnumerable<object> sequence)
        {
            var enumerator = sequence.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var item = (Expression)enumerator.Current;
                if (IsBlockHelper(item, _configuration)
                    && UnwrapStatement(item) is HelperExpression blockHelper)
                {
                    yield return item;

                    string blockName = blockHelper.HelperName
                        .Replace("#", "")
                        .Replace("^", "")
                        .Replace("*", "");

                    foreach (var expression in ExpandBlock(enumerator, blockName))
                    {
                        yield return expression;
                    }

                    if (IsClosingElement((Expression)enumerator.Current, blockName)
                        && enumerator.Current is StatementExpression endStatement)
                    {
                        yield return HandlebarsExpression.Statement(
                            HandlebarsExpression.Path("/" + blockName),
                            endStatement.IsEscaped,
                            endStatement.TrimBefore,
                            endStatement.TrimAfter);
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else
                {
                    yield return item;
                }
            }
        }

        IEnumerable<Expression> ExpandBlock(IEnumerator<object> enumerator, string blockName)
        {
            while (enumerator.MoveNext())
            {
                var item = (Expression)enumerator.Current;

                if (IsClosingElement(item, blockName))
                {
                    yield break;
                }

                if (IsInversionBlock(item)
                    && item is StatementExpression elseStatement
                    && UnwrapStatement(item) is HelperExpression elseHelper
                    && elseHelper.Arguments != null
                    && elseHelper.Arguments.Any())
                {
                    yield return HandlebarsExpression.Statement(
                        HandlebarsExpression.Helper("else"),
                        elseStatement.IsEscaped, elseStatement.TrimBefore, false);

                    string elseBlockName = ((PathExpression)elseHelper.Arguments.First()).Path;
                    yield return HandlebarsExpression.Statement(
                        HandlebarsExpression.Helper(
                            "#" + elseBlockName,
                            elseHelper.Arguments.Skip(1)),
                        elseStatement.IsEscaped, false, elseStatement.TrimAfter);

                    foreach (var expression in ExpandBlock(enumerator, blockName))
                    {
                        yield return expression;
                    }

                    if (IsClosingElement((Expression)enumerator.Current, blockName)
                        && enumerator.Current is StatementExpression endStatement)
                    {
                        yield return HandlebarsExpression.Statement(
                            HandlebarsExpression.Path("/" + elseBlockName),
                            endStatement.IsEscaped,
                            endStatement.TrimBefore,
                            endStatement.TrimAfter);
                        yield break;
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
                else
                {
                    yield return item;
                }
            }
        }

        private static bool IsBlockHelper(Expression item, HandlebarsConfiguration configuration)
        {
            item = UnwrapStatement(item);
            if (item is HelperExpression hitem)
            {
                var helperName = hitem.HelperName;
                return !configuration.Helpers.ContainsKey(helperName) &&
                       configuration.BlockHelpers.ContainsKey(helperName.Replace("#", ""));
            }
            return false;
        }

        private bool IsClosingElement(Expression item, string blockName)
        {
            item = UnwrapStatement(item);
            return item is PathExpression && ((PathExpression)item).Path == "/" + blockName;
        }

        private bool IsInversionBlock(Expression item)
        {
            item = UnwrapStatement(item);
            return item is HelperExpression && ((HelperExpression)item).HelperName == "else";
        }

        private static Expression UnwrapStatement(Expression item)
        {
            if (item is StatementExpression)
            {
                return ((StatementExpression)item).Body;
            }
            else
            {
                return item;
            }
        }
    }
}

