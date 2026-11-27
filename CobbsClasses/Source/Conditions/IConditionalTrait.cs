namespace CobbsClasses.Source.Conditions
{
    public interface IConditionalTrait
    {
        void Evaluate(in ConditionContext context, ConditionalModifiers modifiers);
    }
}