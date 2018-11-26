﻿using LunarLabs.Parser;
using LunarLabs.WebServer.Core;
using System;
using System.Collections;

namespace LunarLabs.WebServer.Templates
{
    internal class TemplateException: Exception
    {
        public TemplateException(string msg): base(msg)
        {

        }
    }

    public class SingleRenderingKey: RenderingKey
    {
        private bool negate;
        private object literal;
        private string global;

        private string[] steps;

        public RenderingType RenderingType { get; private set; }

        internal SingleRenderingKey(string key, RenderingType expectedType)
        {
            negate = false;
            RenderingType = expectedType;

            if (key.StartsWith("!"))
            {
                if (expectedType != RenderingType.Bool && expectedType != RenderingType.Any)
                {
                    throw new Exception("expected bool");
                }

                RenderingType = RenderingType.Bool;
                key = key.Substring(1);
                negate = true;
            }

            if (key.StartsWith("@"))
            {
                global = key.Substring(1);
                return;
            }

            switch (key)
            {
                case "true":
                    if (expectedType != RenderingType.Bool && expectedType != RenderingType.Any)
                    {
                        throw new Exception("expected bool");
                    }

                    RenderingType = RenderingType.Bool;
                    literal = (!negate);
                    return;

                case "false":
                    if (expectedType != RenderingType.Bool && expectedType != RenderingType.Any)
                    {
                        throw new Exception("expected bool");
                    }

                    RenderingType = RenderingType.Bool;
                    literal = negate;
                    return;

                case "this": return;
            }

            if (key.StartsWith("'") && key.EndsWith("'"))
            {
                if (expectedType != RenderingType.String && expectedType != RenderingType.Any)
                {
                    throw new Exception("expected string");
                }

                RenderingType = RenderingType.String;
                literal = key.Substring(1, key.Length - 2);
                return;
            }

            decimal number;
            if (decimal.TryParse(key, out number))
            {
                if (expectedType != RenderingType.Numeric && expectedType != RenderingType.Any)
                {
                    throw new Exception("expected number");
                }

                RenderingType = RenderingType.Numeric;
                literal = number;
                return;
            }

            this.steps = key.Split( '.' );
        }

        public override object Evaluate(RenderingContext context)
        {
            if (literal != null)
            {
                return literal;
            }

            if (global != null)
            {
                return context.Get(global);
            }

            int stackPointer = context.DataStack.Count - 1;
            var obj = context.DataStack[stackPointer];

            if (steps != null)
            {
                // NOTE this while is required for support access to out of scope variables 
                while (stackPointer >= 0)
                {
                    try
                    {
                        for (int i = 0; i < steps.Length; i++)
                        {
                            Type type = obj.GetType();
                            var key = steps[i];

                            var field = type.GetField(key);
                            if (field != null)
                            {
                                obj = field.GetValue(obj);
                                continue;
                            }

                            var prop = type.GetProperty(key);
                            if (prop != null)
                            {
                                obj = prop.GetValue(obj);
                                continue;
                            }

                            if (type == typeof(DataNode))
                            {
                                var node = obj as DataNode;
                                if (node.HasNode(key))
                                {
                                    var val = node.GetNode(key);
                                    if (val != null)
                                    {
                                        if (val.ChildCount > 0)
                                        {
                                            obj = val;
                                        }
                                        else
                                        {
                                            obj = val.Value;
                                        }

                                        continue;
                                    }
                                }

                                throw new TemplateException("node key not found");
                            }

                            IDictionary dict = obj as IDictionary;
                            if (dict != null)
                            {
                                if (dict.Contains(key))
                                {
                                    obj = dict[key];
                                    continue;
                                }

                                type = obj.GetType();
                                Type valueType = type.GetGenericArguments()[1];
                                obj = valueType.GetDefault();
                                continue;
                            }

                            if (key.Equals("count"))
                            {
                                ICollection collection = obj as ICollection;
                                if (collection != null)
                                {
                                    obj = collection.Count;
                                    continue;
                                }

                                throw new TemplateException("count key not found");
                            }

                            throw new TemplateException("key not found");
                        }

                    }
                    catch (TemplateException e)
                    {
                        // if an eval exception was thrown, try searching in the parent scope
                        stackPointer--;
                        if (stackPointer < 0)
                        {
                            throw e;
                        }
                    }
                    break;
                }
            }

            if (negate && obj is bool)
            {
                obj = !((bool)obj);
            }

            return obj;
        }
    }
}