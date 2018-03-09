using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Linq;

namespace Microsoft.Azure.Documents
{
    internal class DocumentDbTranslateExpressionVisitor : ExpressionVisitor
    {
        private List<Type> rootAndChildTypes = new List<Type>();

        private const string ODataConstantParameterizationWrapperTypePrefix = @"System.Web.OData.Query.Expressions.LinqParameterContainer";

        private static bool? NullableBoolStringString(string left, string right)
        {
            // just a stub to build the expression tree with, never gets invoked since the tree is later translated to DocumentDB SQL
            return false;
        }
        private static MethodInfo NullableBoolStringStringMethodInfo = typeof(DocumentDbTranslateExpressionVisitor).GetMethod("NullableBoolStringString", BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Static);
        private static bool BoolStringString(string left, string right)
        {
            // just a stub to build the expression tree with, never gets invoked since the tree is later translated to DocumentDB SQL
            return false;
        }
        private static MethodInfo BoolStringStringMethodInfo = typeof(DocumentDbTranslateExpressionVisitor).GetMethod("BoolStringString", BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Static);

        private void FindAllChildTypes(Type type)
        {
            // don't double-add or recurse infinitely
            if (rootAndChildTypes.Contains(type))
                return;

            // ignore simple value types (including structs)
            if (type.IsValueType)
                return;

            // Avoid certain types that will result in endless recursion
            if (type.FullName == "System.Reflection." + type.Name)
                return;

            // OK, this type is good
            rootAndChildTypes.Add(type);

            // recurse and follow any properties
            PropertyInfo[] properties =
                (from property
                 in type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                 where property.GetIndexParameters().Length == 0
                 select property)
                 .ToArray();
            foreach (PropertyInfo pi in properties)
            {
                FindAllChildTypes(pi.PropertyType);
            }

            // recurse and follow any fields
            IEnumerable<FieldInfo> fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            foreach (FieldInfo fi in fields)
            {
                FindAllChildTypes(fi.FieldType);
            }
        }

        public DocumentDbTranslateExpressionVisitor(Type rootType)
        {
            FindAllChildTypes(rootType);
        }

        /// <summary>
        /// This method will be called for every binary expression in the expression tree.  We evaluate the node and 
        /// convert DateTime or DateTimeOffset comparisons into their string equivalents so they can execute properly
        /// against DocumentDB, which uses JSON, which does not have a DateTime or DateTimeOffset type, only "number"
        /// and "string".
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        protected override Expression VisitBinary(BinaryExpression node)
        {
            // if either left or right are DateTime or DateTimeOffset of the appropriate type
            if (node.Left.Type == typeof(DateTimeOffset) ||
                node.Left.Type == typeof(DateTimeOffset?) ||
                node.Left.Type == typeof(DateTime) ||
                node.Left.Type == typeof(DateTime?) ||
                node.Right.Type == typeof(DateTimeOffset) ||
                node.Right.Type == typeof(DateTimeOffset?) ||
                node.Right.Type == typeof(DateTime) ||
                node.Right.Type == typeof(DateTime?))
            {
                // if this is an operation we can translate to the equivalent string comparison
                if (node.NodeType == ExpressionType.GreaterThan ||
                    node.NodeType == ExpressionType.LessThan ||
                    node.NodeType == ExpressionType.GreaterThanOrEqual ||
                    node.NodeType == ExpressionType.LessThanOrEqual ||
                    node.NodeType == ExpressionType.Equal ||
                    node.NodeType == ExpressionType.NotEqual)
                {
                    // seems the tree is explored root up rather than leaf down
                    // if we just re-make the binary now, before its left/right unarys are also translated, it will still pick up the wrong type to use
                    // note: this method will strip out any nullable conversions meaning we need to add them back in the big if/elseif series below where applicable
                    var leftConverted = TranslateDateTimeToStringType(node.Left);
                    var rightConverted = TranslateDateTimeToStringType(node.Right);

                    // this is not actually a "valid" thing to do in some sense, it makes it non-decomposable back into C# code since "string < | > | <= | >= string" is not a valid expression in C# 
                    // it translates to a DocumentDB SQL query as the DocumentDB visitor walks the tree just fine though, and that's what we actually want
                    if (node.NodeType == ExpressionType.GreaterThan)
                    {
                        if (node.Type == typeof(bool?))
                        {
                            return Expression.GreaterThan(leftConverted, rightConverted, false, NullableBoolStringStringMethodInfo);
                        }
                        else
                        {
                            return Expression.GreaterThan(leftConverted, rightConverted, false, BoolStringStringMethodInfo);
                        }
                    }
                    else if (node.NodeType == ExpressionType.LessThan)
                    {
                        if (node.Type == typeof(bool?))
                        {
                            return Expression.LessThan(leftConverted, rightConverted, false, NullableBoolStringStringMethodInfo);
                        }
                        else
                        {
                            return Expression.LessThan(leftConverted, rightConverted, false, BoolStringStringMethodInfo);
                        }
                    }
                    else if (node.NodeType == ExpressionType.GreaterThanOrEqual)
                    {
                        if (node.Type == typeof(bool?))
                        {
                            return Expression.GreaterThanOrEqual(leftConverted, rightConverted, false, NullableBoolStringStringMethodInfo);
                        }
                        else
                        {
                            return Expression.GreaterThanOrEqual(leftConverted, rightConverted, false, BoolStringStringMethodInfo);
                        }
                    }
                    else if (node.NodeType == ExpressionType.LessThanOrEqual)
                    {
                        if (node.Type == typeof(bool?))
                        {
                            return Expression.LessThanOrEqual(leftConverted, rightConverted, false, NullableBoolStringStringMethodInfo);
                        }
                        else
                        {
                            return Expression.LessThanOrEqual(leftConverted, rightConverted, false, BoolStringStringMethodInfo);
                        }
                    }
                    // since this "==" and "!=" is supported on strings (including against null constants) we can use the simpler version since this would decompose back to valid C# code
                    else if (node.NodeType == ExpressionType.Equal)
                    {
                        if (node.Type == typeof(bool?))
                        {
                            // also convert to nullable
                            return Expression.Convert(Expression.MakeBinary(node.NodeType, leftConverted, rightConverted), typeof(bool?));
                        }
                        else
                        {
                            return Expression.MakeBinary(node.NodeType, leftConverted, rightConverted);
                        }
                    }
                    else if (node.NodeType == ExpressionType.NotEqual)
                    {
                        if (node.Type == typeof(bool?))
                        {
                            // also convert to nullable
                            return Expression.Convert(Expression.MakeBinary(node.NodeType, leftConverted, rightConverted), typeof(bool?));
                        }
                        else
                        {
                            return Expression.MakeBinary(node.NodeType, leftConverted, rightConverted);
                        }
                    }
                }
                else
                {
                    throw new ArgumentException(@"The operation you are attempting to perform on DateTime/Offset is not supported by DocumentDB using ISO 8601 string formatted equivalents. Only '<', '>', '<=', '>=', '==' and '!=' operators are allowed.");
                }
            }

            return base.VisitBinary(node);
        }

        /// <summary>
        /// This converts a unary from DateTime/Offset(?) to string.  If it is a member access on root type being queried or one of its 
        /// child types, we "lie" by fixing up the FieldInfo or PropertyInfo so DocDB "sees" it as a string property in the DB.  If the unary
        /// is a member access on any other type we attempt to execute the unary and format the result as an ISO8601 string.  Constants and 
        /// conditionals (conditionals needed because of some OData oddities) are similarly translated.  This enables the parent binary
        /// expression to be re-written so that the DocumentDB IQueryable implementation will interpret it properly and allow use of various
        /// formerly disallowed operations such as LessThan, etc. on DateTime/Offset(?) DB properties.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private Expression TranslateDateTimeToStringType(Expression node)
        {
            // early exit on constant "null" of type object
            if (node.NodeType == ExpressionType.Constant && (node as ConstantExpression).Value == null)
                return Expression.Constant(null, typeof(string));

            // if not a DateTimeOffset(?) type then do nothing
            if (node.Type != typeof(DateTimeOffset) && node.Type != typeof(DateTimeOffset?) &&
                node.Type != typeof(DateTime) && node.Type != typeof(DateTime?))
            {
                throw new ArgumentException("Node is not of type DateTime or DateTimeOffset (optionally nullable)");
            }

            // remove any surrounding nullable converter, its just an annoyance
            if (node.NodeType == ExpressionType.Convert)
            {
                var unary = (UnaryExpression)node;
                return TranslateDateTimeToStringType(unary.Operand);
            }

            switch (node.NodeType)
            {
                case ExpressionType.Constant:
                    {
                        var constant = node as ConstantExpression;

                        if (constant.Type == typeof(DateTimeOffset))
                        {
                            var formatted = ((DateTimeOffset)constant.Value).ToDocDbFormat();
                            return Expression.Constant(formatted, typeof(string));
                        }
                        else if (constant.Type == typeof(DateTimeOffset?))
                        {
                            var formatted = ((DateTimeOffset?)constant.Value).ToDocDbFormat();
                            return Expression.Constant(formatted, typeof(string));
                        }
                        else if (constant.Type == typeof(DateTime))
                        {
                            var formatted = ((DateTime)constant.Value).ToDocDbFormat();
                            return Expression.Constant(formatted, typeof(string));
                        }
                        else if (constant.Type == typeof(DateTime?))
                        {
                            var formatted = ((DateTime?)constant.Value).ToDocDbFormat();
                            return Expression.Constant(formatted, typeof(string));
                        }

                        throw new ArgumentException("Unable to translate contant node with value '" + constant.Value + "' of type: " + constant.Value.GetType().Name);
                    }

                case ExpressionType.Conditional:
                    {
                        var conditional = node as ConditionalExpression;

                        // convert true/false results to string type
                        var ifTrueConverted = TranslateDateTimeToStringType(conditional.IfTrue);
                        var ifFalseConverted = TranslateDateTimeToStringType(conditional.IfFalse);

                        // rewrite condition with new type, don't touch the test portion
                        return Expression.Condition(conditional.Test, ifTrueConverted, ifFalseConverted);
                    }

                case ExpressionType.MemberAccess:
                    {
                        var nodeAsMemberExpression = node as MemberExpression;

                        // in case of a MemberExpression (accessing a member of an object) of type DateTimeOffset(?), 
                        // ---AND--- it is a member of the root document type or its descendants
                        // ---AND--- it is properly decorated with our custom converter
                        // then modify the MemberExpression to think the member is actually of type string 
                        // (just so DocumentDB outputs the proper SQL when decomposing this expression tree)
                        {
                            // must be part of the type (or its children) that is the target of the query
                            bool isMemberOfQueriedType = rootAndChildTypes.Contains(nodeAsMemberExpression.Member.ReflectedType);

                            // must be decorated properly 
                            var jsonConverter = (JsonConverterAttribute)Attribute.GetCustomAttribute(nodeAsMemberExpression.Member, typeof(JsonConverterAttribute));
                            bool isDecoratedProperly = jsonConverter != null && jsonConverter.ConverterType == typeof(DateTimeDocumentDbJsonConverter);

                            // if its a member of the root type or its children and is decorated properly
                            if (isMemberOfQueriedType && isDecoratedProperly)
                            {
                                // if node is a property member access
                                if (nodeAsMemberExpression.Member is PropertyInfo)
                                {
                                    // wrap the PropertyInfo so it looks like its a string type member
                                    var stringInfo = new PropertyInfoOverrideTypeWrapper(nodeAsMemberExpression.Member as PropertyInfo, typeof(string));

                                    // recreate the node with its new type
                                    var newNode = Expression.MakeMemberAccess(nodeAsMemberExpression.Expression, stringInfo);

                                    // re-write the tree
                                    return newNode;
                                }

                                // if node is a field member access
                                if (nodeAsMemberExpression.Member is FieldInfo)
                                {
                                    // wrap the PropertyInfo so it looks like its a string type member
                                    var stringInfo = new FieldInfoOverrideTypeWrapper(nodeAsMemberExpression.Member as FieldInfo, typeof(string));

                                    // recreate the node with its new type
                                    var newNode = Expression.MakeMemberAccess(nodeAsMemberExpression.Expression, stringInfo);

                                    // re-write the tree
                                    return newNode;
                                }
                            }
                        }

                        // else it is likely an "immediate" in the form of a variable in the Linq expression
                        try
                        {
                            // if its not a member access (or is, but to a member which isn't part of the root document type or its descendants)
                            // then execute this part of the expression tree to get the actual value and convert it to a constant with 
                            // appropriate formatting for DocumentDB string comparison
                            if (node.Type == typeof(DateTimeOffset?))
                            {
                                var lambdaExpression = Expression.Lambda<Func<DateTimeOffset?>>(node);
                                var compiledExpression = lambdaExpression.Compile();
                                var result = compiledExpression();

                                if (result == null)
                                {
                                    // rewrite the tree
                                    return Expression.Constant(null, typeof(string));
                                }

                                var formatted = result.ToDocDbFormat();

                                // rewrite the tree
                                return Expression.Constant(formatted, typeof(string));
                            }
                            else if (node.Type == typeof(DateTimeOffset))
                            {
                                var lambdaExpression = Expression.Lambda<Func<DateTimeOffset>>(node);
                                var compiledExpression = lambdaExpression.Compile();
                                var result = compiledExpression();

                                var formatted = result.ToDocDbFormat();

                                // rewrite the tree
                                return Expression.Constant(formatted, typeof(string));
                            }
                            else if (node.Type == typeof(DateTime?))
                            {
                                var lambdaExpression = Expression.Lambda<Func<DateTime?>>(node);
                                var compiledExpression = lambdaExpression.Compile();
                                var result = compiledExpression();

                                if (result == null)
                                {
                                    // rewrite the tree
                                    return Expression.Constant(null, typeof(string));
                                }

                                var formatted = result.ToDocDbFormat();

                                // rewrite the tree
                                return Expression.Constant(formatted, typeof(string));
                            }
                            else if (node.Type == typeof(DateTime))
                            {
                                var lambdaExpression = Expression.Lambda<Func<DateTime>>(node);
                                var compiledExpression = lambdaExpression.Compile();
                                var result = compiledExpression();

                                var formatted = result.ToDocDbFormat();

                                // rewrite the tree
                                return Expression.Constant(formatted, typeof(string));
                            }
                        }
                        catch
                        {
                        }

                        throw new ArgumentException("Unable to determine how to translate DateTime or DateTimeOffset MemberAccess node - if its part of your document type or DTO model, please decorate it with \"[JsonConverter(typeof(DateTimeDocumentDbJsonConverter))]\"");
                    }

                case ExpressionType.Call:
                    var call = node as MethodCallExpression;

                    // execute this part of the expression tree to get the actual value and convert it to a constant with 
                    // appropriate formatting for DocumentDB string comparison
                    if (node.Type == typeof(DateTimeOffset?))
                    {
                        var lambdaExpression = Expression.Lambda<Func<DateTimeOffset?>>(node);
                        var compiledExpression = lambdaExpression.Compile();
                        var result = compiledExpression();

                        if (result == null)
                        {
                            // rewrite the tree
                            return Expression.Constant(null, typeof(string));
                        }

                        var formatted = result.ToDocDbFormat();

                        // rewrite the tree
                        return Expression.Constant(formatted, typeof(string));
                    }
                    else if (node.Type == typeof(DateTimeOffset))
                    {
                        var lambdaExpression = Expression.Lambda<Func<DateTimeOffset>>(node);
                        var compiledExpression = lambdaExpression.Compile();
                        var result = compiledExpression();

                        var formatted = result.ToDocDbFormat();

                        // rewrite the tree
                        return Expression.Constant(formatted, typeof(string));
                    }
                    else if (node.Type == typeof(DateTime?))
                    {
                        var lambdaExpression = Expression.Lambda<Func<DateTime?>>(node);
                        var compiledExpression = lambdaExpression.Compile();
                        var result = compiledExpression();

                        if (result == null)
                        {
                            // rewrite the tree
                            return Expression.Constant(null, typeof(string));
                        }

                        var formatted = result.ToDocDbFormat();

                        // rewrite the tree
                        return Expression.Constant(formatted, typeof(string));
                    }
                    else if (node.Type == typeof(DateTime))
                    {
                        var lambdaExpression = Expression.Lambda<Func<DateTime>>(node);
                        var compiledExpression = lambdaExpression.Compile();
                        var result = compiledExpression();

                        var formatted = result.ToDocDbFormat();

                        // rewrite the tree
                        return Expression.Constant(formatted, typeof(string));
                    }

                    throw new ArgumentException("Unable to translate Call node with name '" + call.Method.Name + "' of type: " + call.Method.GetType().Name);

                default:
                    throw new ArgumentException("Unable to translate node of type " + node.NodeType);
            }
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            //var lambda = node as LambdaExpression<T>

            // OData wraps anonymous type instantiation ".Select()" in this when applying the URI to the IQueryable
            if (node.Type.Name.StartsWith("Func") &&
                node.ReturnType.Name.StartsWith("SelectSomeAndInheritance") &&
                node.Body.NodeType == ExpressionType.MemberInit)
            {
                var memberInit = node.Body as MemberInitExpression;

                //return Expression.Lambda<T>(new Expression(), params)
            }

            return base.VisitLambda(node);
        }

        /// <summary>
        /// When using this translator with OData, those libraries may insert "non-standard" nodes which enable them to do constant parameterization.
        /// This simply allows us to check whether that is happening and emit an error with instructions on how to make the two work together.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Type.FullName.StartsWith(ODataConstantParameterizationWrapperTypePrefix))
            {
                throw new ArgumentException("Usage of this query rewriter is incompatible with OData's EnableConstantParameterization = true option.");
            }

            return base.VisitConstant(node);
        }
    }
}
