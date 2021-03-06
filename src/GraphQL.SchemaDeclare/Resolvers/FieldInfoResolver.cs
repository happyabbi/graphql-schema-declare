﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GraphQL.Resolvers;
using GraphQL.SchemaDeclare.GenerationServices;
using GraphQL.Types;

namespace GraphQL.SchemaDeclare.Resolvers
{
	public class FieldInfoResolver : IFieldInfoResolver
	{
		private readonly IDependencyResolver dependencyResolver;

		public FieldInfoResolver(IDependencyResolver dependencyResolver)
		{
			this.dependencyResolver = dependencyResolver;
		}

		private object GetArgumentValueFromParameterInfo(ResolveFieldContext<object> context, System.Reflection.ParameterInfo parameter)
		{
			if (context.Arguments is null)
			{
				var isNotSet = parameter.DefaultValue is System.DBNull;
				if (isNotSet)
					return null;
				return parameter.DefaultValue;
			}

			var pType = parameter.ParameterType;
			var argName = parameter.Name.ToCamelCase();
			var arg = context.Arguments.FirstOrDefault(_ => _.Key == argName);

			var value = arg.Value is System.DBNull ?
				null :
				context.GetArgument(pType, argName);

			return value;
		}

		private object[] GetParametersValue(ResolveFieldContext<object> context, System.Reflection.MethodInfo methodInfo)
		{
			var list = new List<object>();
			foreach (var parameter in methodInfo.GetParameters())
			{
				var value = this.GetArgumentValueFromParameterInfo(context, parameter);
				list.Add(value);
			}

			return list.ToArray();
		}

		protected virtual object GetController(IGraphType graphType, FieldInfo fieldInfo)
		{
			var canUseDependencyResolver = this.dependencyResolver != null;

			if (canUseDependencyResolver)
			{
				return this.dependencyResolver.Resolve(fieldInfo.ControllerType);
			}
			return Activator.CreateInstance(fieldInfo.ControllerType);
		}

		private async Task<object> InvokeAsync(System.Reflection.MethodInfo methodInfo, object obj, object[] parameters)
		{
			dynamic awaitable = methodInfo.Invoke(obj, parameters);
			await awaitable;
			return awaitable.GetAwaiter().GetResult();
		}

		protected virtual Func<ResolveFieldContext<object>, Task<object>> GenerateFunctionResolveAsync(IGraphType graphType, FieldInfo fieldInfo)
		{
			return async (ResolveFieldContext<object> context) =>
			{
				var controller = this.GetController(graphType, fieldInfo);

				var objects = this.GetParametersValue(context, fieldInfo.Action);

				return await InvokeAsync(fieldInfo.Action, controller, objects);
			};
		}

		protected virtual Func<ResolveFieldContext<object>, object> GenerateFunctionResolve(IGraphType graphType, FieldInfo fieldInfo)
		{
			return (ResolveFieldContext<object> context) =>
			{
				var controller = this.GetController(graphType, fieldInfo);

				var objects = this.GetParametersValue(context, fieldInfo.Action);

				return fieldInfo.Action.Invoke(controller, objects);
			};
		}

		public IFieldResolver GenerateFieldResolver(IGraphType graphType, FieldInfo fieldInfo)
		{
			return new FuncFieldResolver<object>(this.GenerateFunctionResolve(graphType, fieldInfo));
		}

		public IFieldResolver GenerateFieldResolverAsync(IComplexGraphType graphType, FieldInfo fieldInfo)
		{
			return new AsyncFieldResolver<object>(this.GenerateFunctionResolveAsync(graphType, fieldInfo));
		}
	}
}
