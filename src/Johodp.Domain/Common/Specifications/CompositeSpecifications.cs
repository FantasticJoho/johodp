namespace Johodp.Domain.Common.Specifications;

using System.Linq.Expressions;

/// <summary>
/// Combines two specifications with AND logic
/// </summary>
public sealed class AndSpecification<T> : Specification<T> where T : class
{
    public AndSpecification(Specification<T> left, Specification<T> right)
    {
        if (left.Criteria is null || right.Criteria is null)
        {
            throw new ArgumentException("Both specifications must have criteria");
        }

        var parameter = Expression.Parameter(typeof(T));
        
        var leftVisitor = new ParameterReplacer(parameter);
        var leftExpression = leftVisitor.Visit(left.Criteria.Body);

        var rightVisitor = new ParameterReplacer(parameter);
        var rightExpression = rightVisitor.Visit(right.Criteria.Body);

        var andExpression = Expression.AndAlso(leftExpression!, rightExpression!);
        Criteria = Expression.Lambda<Func<T, bool>>(andExpression, parameter);

        // Combine includes
        Includes.AddRange(left.Includes);
        Includes.AddRange(right.Includes);
        IncludeStrings.AddRange(left.IncludeStrings);
        IncludeStrings.AddRange(right.IncludeStrings);
    }

    private class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter;

        public ParameterReplacer(ParameterExpression parameter)
        {
            _parameter = parameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return _parameter;
        }
    }
}

/// <summary>
/// Combines two specifications with OR logic
/// </summary>
public sealed class OrSpecification<T> : Specification<T> where T : class
{
    public OrSpecification(Specification<T> left, Specification<T> right)
    {
        if (left.Criteria is null || right.Criteria is null)
        {
            throw new ArgumentException("Both specifications must have criteria");
        }

        var parameter = Expression.Parameter(typeof(T));
        
        var leftVisitor = new ParameterReplacer(parameter);
        var leftExpression = leftVisitor.Visit(left.Criteria.Body);

        var rightVisitor = new ParameterReplacer(parameter);
        var rightExpression = rightVisitor.Visit(right.Criteria.Body);

        var orExpression = Expression.OrElse(leftExpression!, rightExpression!);
        Criteria = Expression.Lambda<Func<T, bool>>(orExpression, parameter);

        // Combine includes
        Includes.AddRange(left.Includes);
        Includes.AddRange(right.Includes);
        IncludeStrings.AddRange(left.IncludeStrings);
        IncludeStrings.AddRange(right.IncludeStrings);
    }

    private class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter;

        public ParameterReplacer(ParameterExpression parameter)
        {
            _parameter = parameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return _parameter;
        }
    }
}

/// <summary>
/// Negates a specification with NOT logic
/// </summary>
public sealed class NotSpecification<T> : Specification<T> where T : class
{
    public NotSpecification(Specification<T> specification)
    {
        if (specification.Criteria is null)
        {
            throw new ArgumentException("Specification must have criteria");
        }

        var parameter = Expression.Parameter(typeof(T));
        
        var visitor = new ParameterReplacer(parameter);
        var expression = visitor.Visit(specification.Criteria.Body);

        var notExpression = Expression.Not(expression!);
        Criteria = Expression.Lambda<Func<T, bool>>(notExpression, parameter);

        // Copy includes
        Includes.AddRange(specification.Includes);
        IncludeStrings.AddRange(specification.IncludeStrings);
    }

    private class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter;

        public ParameterReplacer(ParameterExpression parameter)
        {
            _parameter = parameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return _parameter;
        }
    }
}
