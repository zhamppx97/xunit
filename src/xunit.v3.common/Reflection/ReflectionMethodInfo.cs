﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit.Abstractions;
using Xunit.Internal;

namespace Xunit.Sdk
{
	/// <summary>
	/// Reflection-based implementation of <see cref="IReflectionMethodInfo"/>.
	/// </summary>
	public class ReflectionMethodInfo : IReflectionMethodInfo
	{
		static readonly IEqualityComparer TypeComparer = new GenericTypeComparer();

		IEnumerable<IParameterInfo>? cachedParameters = null;

		/// <summary>
		/// Initializes a new instance of the <see cref="ReflectionMethodInfo"/> class.
		/// </summary>
		/// <param name="method">The method to be wrapped.</param>
		public ReflectionMethodInfo(MethodInfo method)
		{
			MethodInfo = Guard.ArgumentNotNull(nameof(method), method);
		}

		/// <inheritdoc/>
		public bool IsAbstract => MethodInfo.IsAbstract;

		/// <inheritdoc/>
		public bool IsGenericMethodDefinition => MethodInfo.IsGenericMethodDefinition;

		/// <inheritdoc/>
		public bool IsPublic => MethodInfo.IsPublic;

		/// <inheritdoc/>
		public bool IsStatic => MethodInfo.IsStatic;

		/// <inheritdoc/>
		public MethodInfo MethodInfo { get; }

		/// <inheritdoc/>
		public string Name => MethodInfo.Name;

		/// <inheritdoc/>
		public ITypeInfo ReturnType => Reflector.Wrap(MethodInfo.ReturnType);

		/// <inheritdoc/>
		public ITypeInfo Type => Reflector.Wrap(MethodInfo.ReflectedType!);

		/// <inheritdoc/>
		public IEnumerable<IAttributeInfo> GetCustomAttributes(string assemblyQualifiedAttributeTypeName)
		{
			Guard.ArgumentNotNull(nameof(assemblyQualifiedAttributeTypeName), assemblyQualifiedAttributeTypeName);

			return GetCustomAttributes(MethodInfo, assemblyQualifiedAttributeTypeName).CastOrToList();
		}

		static IEnumerable<IAttributeInfo> GetCustomAttributes(
			MethodInfo method,
			string assemblyQualifiedAttributeTypeName)
		{
			Guard.ArgumentNotNull(nameof(method), method);
			Guard.ArgumentNotNull(nameof(assemblyQualifiedAttributeTypeName), assemblyQualifiedAttributeTypeName);

			var attributeType = ReflectionAttributeNameCache.GetType(assemblyQualifiedAttributeTypeName);

			Guard.ArgumentValidNotNull(nameof(assemblyQualifiedAttributeTypeName), $"Could not load type: '{assemblyQualifiedAttributeTypeName}'", attributeType);

			return GetCustomAttributes(method, attributeType, ReflectionAttributeInfo.GetAttributeUsage(attributeType));
		}

		static IEnumerable<IAttributeInfo> GetCustomAttributes(
			MethodInfo method,
			Type attributeType,
			AttributeUsageAttribute attributeUsage)
		{
			List<ReflectionAttributeInfo>? list = null;

			foreach (var attr in method.CustomAttributes)
			{
				if (attributeType.IsAssignableFrom(attr.AttributeType))
				{
					if (list == null)
						list = new List<ReflectionAttributeInfo>();

					list.Add(new ReflectionAttributeInfo(attr));
				}
			}

			if (list != null)
				list.Sort((left, right) => string.Compare(left.AttributeData.AttributeType.Name, right.AttributeData.AttributeType.Name, StringComparison.Ordinal));

			var results = list ?? Enumerable.Empty<IAttributeInfo>();

			if (attributeUsage.Inherited && (attributeUsage.AllowMultiple || list == null))
			{
				// Need to find the parent method, which may not necessarily be on the parent type
				var baseMethod = GetParent(method);
				if (baseMethod != null)
					results = results.Concat(GetCustomAttributes(baseMethod, attributeType, attributeUsage));
			}

			return results;
		}

		/// <inheritdoc/>
		public IEnumerable<ITypeInfo> GetGenericArguments() =>
			MethodInfo.GetGenericArguments().Select(t => Reflector.Wrap(t)).ToArray();

		static MethodInfo? GetParent(MethodInfo method)
		{
			if (!method.IsVirtual)
				return null;

			var methodParameters = method.GetParameters();
			var methodGenericArgCount = method.GetGenericArguments().Length;

			var currentType = method.DeclaringType;

			while (currentType != typeof(object) && currentType != null)
			{
				currentType = currentType.BaseType;
				if (currentType == null)
					return null;

				foreach (var m in currentType.GetMatchingMethods(method))
				{
					if (m.Name == method.Name &&
						m.GetGenericArguments().Length == methodGenericArgCount &&
						ParametersHaveSameTypes(methodParameters, m.GetParameters()))
						return m;
				}
			}

			return null;
		}

		static bool ParametersHaveSameTypes(
			ParameterInfo[] left,
			ParameterInfo[] right)
		{
			if (left.Length != right.Length)
				return false;

			for (var i = 0; i < left.Length; i++)
				if (!TypeComparer.Equals(left[i].ParameterType, right[i].ParameterType))
					return false;

			return true;
		}

		/// <inheritdoc/>
		public IMethodInfo MakeGenericMethod(params ITypeInfo[] typeArguments)
		{
			Guard.ArgumentNotNull(nameof(typeArguments), typeArguments);

			var unwrapedTypeArguments = typeArguments.Select(t => ((IReflectionTypeInfo)t).Type).ToArray();

			return Reflector.Wrap(MethodInfo.MakeGenericMethod(unwrapedTypeArguments));
		}

		/// <inheritdoc/>
		public override string? ToString() => MethodInfo.ToString();

		/// <inheritdoc/>
		public IEnumerable<IParameterInfo> GetParameters()
		{
			if (cachedParameters == null)
			{
				var parameters = MethodInfo.GetParameters();
				var parameterInfos = new IParameterInfo[parameters.Length];

				for (var i = 0; i < parameterInfos.Length; i++)
					parameterInfos[i] = Reflector.Wrap(parameters[i]);

				cachedParameters = parameterInfos;
			}

			return cachedParameters;
		}

		class GenericTypeComparer : IEqualityComparer
		{
			bool IEqualityComparer.Equals(object? x, object? y)
			{
				if (x == null && y == null)
					return true;
				if (x == null || y == null)
					return false;

				var typeX = (Type)x;
				var typeY = (Type)y;

				if (typeX.IsGenericParameter && typeY.IsGenericParameter)
					return typeX.GenericParameterPosition == typeY.GenericParameterPosition;

				return typeX == typeY;
			}

			int IEqualityComparer.GetHashCode(object obj)
			{
				throw new NotImplementedException();
			}
		}
	}
}
