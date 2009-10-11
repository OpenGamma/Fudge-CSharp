﻿/**
 * Copyright (C) 2009 - 2009 by OpenGamma Inc. and other contributors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 *     
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.Reflection;
using IQToolkit;
using System.Diagnostics;

namespace OpenGamma.Fudge.Linq
{
    /// <summary>
    /// Used to translate <see cref="Expression"/>s so that calls to get values from members of the
    /// reference type become <c>GetValue</c> calls on the <see cref="FudgeMsg"/> instead.
    /// </summary>
    internal class FudgeExpressionTranslator : ExpressionVisitor
    {
        private static MethodInfo getValueMethod = typeof(FudgeMsg).GetMethod("GetValue", new Type[] { typeof(string), typeof(Type) });
        private readonly ParameterExpression msgParam;
        private readonly IEnumerable<FudgeMsg> source;
        private readonly Type dataType;

        public FudgeExpressionTranslator(Type dataType, ParameterExpression msgParam, IEnumerable<FudgeMsg> source)
        {
            this.dataType = dataType;
            this.msgParam = msgParam;
            this.source = source;
        }

        public Expression Translate(Expression exp)
        {
            var newExp = this.Visit(exp);
            return newExp;
        }

        public static Expression StripQuotes(Expression exp)
        {
            while (exp.NodeType == ExpressionType.Quote)
                exp = ((UnaryExpression)exp).Operand;
            return exp;
        }

        protected override Expression VisitConstant(ConstantExpression c)
        {
            // This is where we switch the ultimate source used by the expression tree from the Query<type> to our IEnumerable<FudgeMsg>
            if (c.Type.IsGenericType && c.Type.GetGenericTypeDefinition() == typeof(Query<>) && c.Type.GetGenericArguments()[0] == dataType)
            {
                return Expression.Constant(source, typeof(IEnumerable<>).MakeGenericType(typeof(FudgeMsg)));
            }

            return base.VisitConstant(c);
        }

        protected override Expression VisitLambda(LambdaExpression lambda)
        {
            // If we have a lambda of the form dataType => something then it now becomes FudgeMsg => something
            var body = Visit(lambda.Body);
            IList<ParameterExpression> parameters = lambda.Parameters;
            if (parameters.Count == 1 && parameters[0].Type == dataType)
            {
                return Expression.Lambda(body, msgParam);
            }
            return UpdateLambda(lambda, lambda.Type, body, parameters);
        }

        protected override Expression VisitMethodCall(MethodCallExpression m)
        {
            // Translate calls to Queryable.xxx<IQueryable<dataType>(...) to Enumerable.xxx<IEnumerable<FudgeMsg>>(...)
            // TODO t0rx 20091011 -- Need to refactor this to make less copy-paste and more efficient
            Expression obj = Visit(m.Object);
            var args = VisitExpressionList(m.Arguments);

            var method = m.Method;
            if (method.DeclaringType == typeof(Queryable))
            {
                var newArgs = (from arg in args select StripQuotes(arg)).ToArray();     // Get rid of pesky quotes
                switch (method.Name)
                {
                    case "Select":
                        {
                            Debug.Assert(newArgs.Length == 2);
                            // Find Enumerable.Select(IEnumerable<TSource>,Func<TSource,TResult>)
                            // [rather than Func<TSource,Int32,Boolean> variant]
                            var genericMethod = (from mi in typeof(Enumerable).GetMethods()
                                                 where mi.Name == method.Name && mi.GetParameters()[1].ParameterType.GetGenericArguments().Length == 2
                                                 select mi).Single();
                            var newMethod = genericMethod.MakeGenericMethod(newArgs[1].Type.GetGenericArguments());     // type args for method are same as for our func
                            return Expression.Call(newMethod, newArgs);
                        }
                    case "Where":
                        {
                            Debug.Assert(newArgs.Length == 2);
                            // Find Enumerable.Where(IEnumerable<TSource>,Func<TSource,Boolean>)
                            // [rather than Func<TSource,Int32,Boolean> variant]
                            var genericMethod = (from mi in typeof(Enumerable).GetMethods()
                                                 where mi.Name == method.Name && mi.GetParameters()[1].ParameterType.GetGenericArguments().Length == 2
                                                 select mi).Single();
                            var newMethod = genericMethod.MakeGenericMethod(new Type[] { typeof(FudgeMsg) });
                            return Expression.Call(newMethod, newArgs);
                        }
                    case "OrderBy":
                    case "OrderByDescending":
                        {
                            Debug.Assert(newArgs.Length == 2);
                            // Find Enumerable.OrderBy(IEnumerable<TSource>,Func<TSource,TKey>)
                            var genericMethod = (from mi in typeof(Enumerable).GetMethods()
                                                 where mi.Name == method.Name && mi.GetParameters().Length == 2
                                                 select mi).Single();
                            var newMethod = genericMethod.MakeGenericMethod(newArgs[1].Type.GetGenericArguments());     // type args for method are same as for our func
                            return Expression.Call(newMethod, newArgs);
                        }
                    default:
                        break;
                }
            }
            return UpdateMethodCall(m, obj, method, args);
        }

        protected override Expression VisitMemberAccess(MemberExpression m)
        {
            // Pick up accesses to dataType.member and translate to FudgeMsg.GetValue(membername)
            if (m.Expression != null && m.Expression.NodeType == ExpressionType.Parameter && m.Expression.Type == dataType)
            {
                // Change the member access to the data type into a call to get the value from the message
                return Expression.Convert(Expression.Call(msgParam, getValueMethod, Expression.Constant(m.Member.Name), Expression.Constant(m.Type)), m.Type);
            }
            else
            {
                return base.VisitMemberAccess(m);
            }
        }
    }
}