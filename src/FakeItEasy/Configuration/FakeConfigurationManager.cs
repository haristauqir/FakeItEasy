namespace FakeItEasy.Configuration
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Expressions;
    using FakeItEasy.Core;

    internal class FakeConfigurationManager
        : IFakeConfigurationManager
    {
        private readonly IConfigurationFactory configurationFactory;
        private readonly ICallExpressionParser callExpressionParser;
        private readonly IInterceptionAsserter interceptionAsserter;
        private readonly ExpressionCallRule.Factory ruleFactory;

        public FakeConfigurationManager(IConfigurationFactory configurationFactory, ExpressionCallRule.Factory callRuleFactory, ICallExpressionParser callExpressionParser, IInterceptionAsserter interceptionAsserter)
        {
            this.configurationFactory = configurationFactory;
            this.ruleFactory = callRuleFactory;
            this.callExpressionParser = callExpressionParser;
            this.interceptionAsserter = interceptionAsserter;
        }

        public IVoidArgumentValidationConfiguration CallTo(Expression<Action> callSpecification)
        {
            Guard.AgainstNull(callSpecification, "callSpecification");

            var parsedCallExpression = this.callExpressionParser.Parse(callSpecification);
            EnsureCallIsMadeOnAFake(parsedCallExpression);
            this.AssertThatMemberCanBeIntercepted(parsedCallExpression);

            return this.CreateVoidArgumentValidationConfiguration(parsedCallExpression);
        }

        [SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "This is by design when using the Expression-, Action- and Func-types.")]
        public IReturnValueArgumentValidationConfiguration<T> CallTo<T>(Expression<Func<T>> callSpecification)
        {
            Guard.AgainstNull(callSpecification, "callSpecification");

            var parsedCallExpression = this.callExpressionParser.Parse(callSpecification);
            var fake = GetFakeManagerCallIsMadeOn(parsedCallExpression);
            this.AssertThatMemberCanBeIntercepted(parsedCallExpression);

            var rule = this.ruleFactory.Invoke(parsedCallExpression);

            fake.AddRuleFirst(rule);

            return this.configurationFactory.CreateConfiguration<T>(fake, rule);
        }

        public IAnyCallConfigurationWithNoReturnTypeSpecified CallTo(object fakeObject)
        {
            var rule = new AnyCallCallRule();
            var manager = Fake.GetFakeManager(fakeObject);
            manager.AddRuleFirst(rule);

            return this.configurationFactory.CreateAnyCallConfiguration(manager, rule);
        }

        public IPropertySetterAnyValueConfiguration<TValue> CallToSet<TValue>(Expression<Func<TValue>> propertySpecification)
        {
            Guard.AgainstNull(propertySpecification, nameof(propertySpecification));
            var parsedCallExpression = this.callExpressionParser.Parse(propertySpecification);
            EnsureCallIsMadeOnAFake(parsedCallExpression);
            this.AssertThatMemberCanBeIntercepted(parsedCallExpression);

            var setterExpression = BuildSetterFromGetter<TValue>(parsedCallExpression);

            return new PropertySetterConfiguration<TValue>(
                setterExpression,
                lambda => this.CreateVoidArgumentValidationConfiguration(this.callExpressionParser.Parse(lambda)));
        }

        private static string GetPropertyName(ParsedCallExpression parsedCallExpression)
        {
            var calledMethod = parsedCallExpression.CalledMethod;
            if (HasThis(calledMethod) && calledMethod.IsSpecialName)
            {
                var methodName = calledMethod.Name;
                if (methodName.StartsWith("get_", StringComparison.Ordinal))
                {
                    return methodName.Substring(4);
                }
            }

            return null;
        }

        private static bool HasThis(MethodInfo methodCall)
        {
            return methodCall.CallingConvention.HasFlag(CallingConventions.HasThis);
        }

        private static Expression BuildArgumentThatMatchesAnything<TValue>()
        {
            Expression<Func<TValue>> value = () => A<TValue>.Ignored;
            return value.Body;
        }

        private static FakeManager GetFakeManagerCallIsMadeOn(ParsedCallExpression parsedCallExpression)
        {
            return parsedCallExpression.CallTarget == null
                ? null
                : Fake.GetFakeManager(parsedCallExpression.CallTarget);
        }

        private static void EnsureCallIsMadeOnAFake(ParsedCallExpression parsedCallExpression)
        {
            GetFakeManagerCallIsMadeOn(parsedCallExpression);
        }

        private static string GetExpressionDescription(ParsedCallExpression parsedCallExpression)
        {
            var matcher = new ExpressionCallMatcher(
                parsedCallExpression,
                ServiceLocator.Current.Resolve<ExpressionArgumentConstraintFactory>(),
                ServiceLocator.Current.Resolve<MethodInfoManager>());

            return matcher.DescriptionOfMatchingCall;
        }

        private static MethodCallExpression BuildSetterFromGetter<TValue>(
            ParsedCallExpression parsedCallExpression)
        {
            var propertyName = GetPropertyName(parsedCallExpression);
            if (propertyName == null)
            {
                var expressionDescription = GetExpressionDescription(parsedCallExpression);
                throw new ArgumentException("Expression '" + expressionDescription +
                                            "' must refer to a property or indexer getter, but doesn't.");
            }

            var parsedArgumentExpressions = parsedCallExpression.ArgumentsExpressions ?? new ParsedArgumentExpression[0];
            var parameterTypes = parsedArgumentExpressions
                .Select(p => p.ArgumentInformation.ParameterType)
                .Concat(new[] { parsedCallExpression.CalledMethod.ReturnType })
                .ToArray();

            var indexerSetterInfo = parsedCallExpression.CallTarget.GetType()
                .GetMethod("set_" + propertyName, parameterTypes);

            if (indexerSetterInfo == null)
            {
                if (parsedArgumentExpressions.Any())
                {
                    var expressionDescription = GetExpressionDescription(parsedCallExpression);
                    throw new ArgumentException("Expression '" + expressionDescription +
                                                "' refers to an indexed property that does not have a setter.");
                }

                throw new ArgumentException(
                    "The property '" + propertyName + "' does not have a setter.");
            }

            var arguments = parsedArgumentExpressions
                .Select(a => a.Expression)
                .Concat(new[] { BuildArgumentThatMatchesAnything<TValue>() });

            return Expression.Call(
                Expression.Constant(parsedCallExpression.CallTarget),
                indexerSetterInfo,
                arguments);
        }

        private IVoidArgumentValidationConfiguration CreateVoidArgumentValidationConfiguration(ParsedCallExpression parsedCallExpression)
        {
            var fake = GetFakeManagerCallIsMadeOn(parsedCallExpression);
            var rule = this.ruleFactory.Invoke(parsedCallExpression);
            fake.AddRuleFirst(rule);

            return this.configurationFactory.CreateConfiguration(fake, rule);
        }

        private void AssertThatMemberCanBeIntercepted(ParsedCallExpression parsed)
        {
            this.interceptionAsserter.AssertThatMethodCanBeInterceptedOnInstance(
                parsed.CalledMethod,
                parsed.CallTarget);
        }
    }
}
