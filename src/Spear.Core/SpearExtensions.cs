﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Spear.Core
{
    internal static class SpearExtensions
    {
        private const string IpRegex = @"^((2[0-4]\d|25[0-5]|[01]?\d\d?)\.){3}(2[0-4]\d|25[0-5]|[01]?\d\d?)$";

        public static bool IsIp(this string str)
        {
            return !string.IsNullOrWhiteSpace(str) && Regex.IsMatch(str, IpRegex);
        }

        

        /// <summary> 随机排序 </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        /// <returns></returns>
        public static IEnumerable<T> RandomSort<T>(this IEnumerable<T> array)
        {
            return array.OrderBy(t => Guid.NewGuid());
        }

        /// <summary> 异常信息格式化 </summary>
        /// <param name="ex"></param>
        /// <param name="isHideStackTrace"></param>
        /// <returns></returns>
        public static string Format(this Exception ex, bool isHideStackTrace = false)
        {
            var sb = new StringBuilder();
            var count = 0;
            var appString = string.Empty;
            while (ex != null)
            {
                if (count > 0)
                {
                    appString += "  ";
                }
                sb.AppendLine($"{appString}异常消息：{ex.Message}");
                sb.AppendLine($"{appString}异常类型：{ex.GetType().FullName}");
                sb.AppendLine($"{appString}异常方法：{(ex.TargetSite == null ? null : ex.TargetSite.Name)}");
                sb.AppendLine($"{appString}异常源：{ex.Source}");
                if (!isHideStackTrace && ex.StackTrace != null)
                {
                    sb.AppendLine($"{appString}异常堆栈：{ex.StackTrace}");
                }
                if (ex.InnerException != null)
                {
                    sb.AppendLine($"{appString}内部异常：");
                    count++;
                }
                ex = ex.InnerException;
            }
            return sb.ToString();
        }

        /// <summary>
        /// 判断类型是否为Nullable类型
        /// </summary>
        /// <param name="type"> 要处理的类型 </param>
        /// <returns> 是返回True，不是返回False </returns>
        public static bool IsNullableType(this Type type)
        {
            return ((type != null) && type.IsGenericType) && (type.GetGenericTypeDefinition() == typeof(Nullable<>));
        }

        /// <summary>
        /// 通过类型转换器获取Nullable类型的基础类型
        /// </summary>
        /// <param name="type"> 要处理的类型对象 </param>
        /// <returns> </returns>
        public static Type GetUnNullableType(this Type type)
        {
            if (!IsNullableType(type)) return type;
            var nullableConverter = new NullableConverter(type);
            return nullableConverter.UnderlyingType;
        }

        /// <summary>
        /// 对象转换为泛型
        /// </summary>
        /// <param name="obj"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T CastTo<T>(this object obj)
        {
            return obj.CastTo(default(T));
        }

        /// <summary>
        /// 对象转换为泛型
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="def"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T CastTo<T>(this object obj, T def)
        {
            var value = obj.CastTo(typeof(T));
            if (value == null)
                return def;
            return (T)value;
        }

        /// <summary> 把对象类型转换为指定类型 </summary>
        /// <param name="obj"></param>
        /// <param name="conversionType"></param>
        /// <returns></returns>
        public static object CastTo(this object obj, Type conversionType)
        {
            if (obj == null)
            {
                return conversionType.IsGenericType ? Activator.CreateInstance(conversionType) : null;
            }
            if (conversionType.IsNullableType())
                conversionType = conversionType.GetUnNullableType();
            try
            {
                if (conversionType == obj.GetType())
                    return obj;
                if (conversionType.IsEnum)
                {
                    return obj is string s
                        ? Enum.Parse(conversionType, s)
                        : Enum.ToObject(conversionType, obj);
                }

                if (!conversionType.IsInterface && conversionType.IsGenericType)
                {
                    var innerType = conversionType.GetGenericArguments()[0];
                    var innerValue = CastTo(obj, innerType);
                    return Activator.CreateInstance(conversionType, innerValue);
                }

                if (conversionType == typeof(Guid))
                {
                    if (Guid.TryParse(obj.ToString(), out var guid))
                        return guid;
                    return null;
                }

                if (conversionType == typeof(Version))
                {
                    if (Version.TryParse(obj.ToString(), out var version))
                        return version;
                    return null;
                }

                return !(obj is IConvertible) ? obj : Convert.ChangeType(obj, conversionType);
            }
            catch
            {
                return null;
            }
        }
    }
}
