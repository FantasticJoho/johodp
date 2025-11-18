namespace Johodp.Domain.Common.Specifications;

using System.Linq.Expressions;

/// <summary>
/// Spécification avec évaluation customisée et résultat projeté
/// </summary>
public abstract class Specification<T, TResult> : Specification<T> where T : class
{
    public Expression<Func<T, TResult>>? Select { get; protected set; }

    protected virtual void ApplySelect(Expression<Func<T, TResult>> selectExpression)
    {
        Select = selectExpression;
    }
}
