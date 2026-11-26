namespace CobbsClasses.Source.Conditions
{
    internal interface IConditionalTrait
    {
        void Evaluate(in ConditionContext context, ConditionalModifiers modifiers);
    }
}