namespace FakeItEasy.Configuration
{
    using System;
    using System.Collections.Generic;
    using FakeItEasy.Core;

    internal class RecordingRuleBuilder
        : IRecordingConfigurationWithArgumentValidation
    {
        private readonly RecordedCallRule rule;
        private readonly RuleBuilder wrappedBuilder;

        public RecordingRuleBuilder(RecordedCallRule rule, RuleBuilder wrappedBuilder)
        {
            this.rule = rule;
            this.wrappedBuilder = wrappedBuilder;
        }

        public delegate RecordingRuleBuilder Factory(RecordedCallRule rule, FakeManager fakeObject);

        public IAfterCallSpecifiedConfiguration DoesNothing()
        {
            return this.wrappedBuilder.DoesNothing();
        }

        public IAfterCallSpecifiedConfiguration Throws(Func<IFakeObjectCall, Exception> exceptionFactory)
        {
            return this.wrappedBuilder.Throws(exceptionFactory);
        }

        public IVoidConfiguration Invokes(Action<IFakeObjectCall> action)
        {
            return this.wrappedBuilder.Invokes(action);
        }

        public IAfterCallSpecifiedConfiguration CallsBaseMethod()
        {
            return this.wrappedBuilder.CallsBaseMethod();
        }

        public IAfterCallSpecifiedConfiguration AssignsOutAndRefParametersLazily(Func<IFakeObjectCall, ICollection<object>> valueProducer)
        {
            return this.wrappedBuilder.AssignsOutAndRefParametersLazily(valueProducer);
        }

        public IAfterMustHaveHappenedConfiguration MustHaveHappened(Repeated repeatConstraint)
        {
            Guard.AgainstNull(repeatConstraint, "repeatConstraint");

            this.rule.RepeatConstraint = repeatConstraint;
            this.rule.IsAssertion = true;
            return new AfterMustHaveHappenedRecordedCallConfiguration(this.rule);
        }

        public IRecordingConfiguration WhenArgumentsMatch(Func<ArgumentCollection, bool> argumentsPredicate)
        {
            Guard.AgainstNull(argumentsPredicate, "argumentsPredicate");

            this.rule.UsePredicateToValidateArguments(argumentsPredicate);

            return this;
        }

        private class AfterMustHaveHappenedRecordedCallConfiguration : IAfterMustHaveHappenedConfiguration
        {
            private readonly RecordedCallRule rule;

            public AfterMustHaveHappenedRecordedCallConfiguration(RecordedCallRule rule)
            {
                this.rule = rule;
            }

            public void InOrder(ISequentialCallContext context)
            {
                Guard.AgainstNull(context, "context");

                this.rule.SequentialCallContext = context;
            }
        }
    }
}
