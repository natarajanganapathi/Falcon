namespace Falcon.Infrastructure.EntityFramework.QueryBuilder;

public static class FilterExtensions
{
    private static readonly MethodInfo ContainsMethod = Types.EnumerableType.GetMethods()
                        .Single(m => m.Name == Names.EnumerableContains && m.GetParameters().Length == 2);

    public static Expression<Func<TEntity, bool>>? GetFilterExpression<TEntity>(this IFilter filter)
    {
        return filter switch
        {
            ICompositeFilter compositeFilter => compositeFilter.GetFilterExpression<TEntity>(),
            IFieldFilter fieldFilter => fieldFilter.GetFilterExpression<TEntity>(),
            IUnaryFilter unaryFilter => unaryFilter.GetFilterExpression<TEntity>(),
            _ => throw new QueryBuilderException(new StringBuilder().Append("Filter type ").Append(filter.GetType().Name).Append("is not supported").ToString())
        };
    }
    private static Expression<Func<TEntity, bool>>? GetFilterExpression<TEntity>(this ICompositeFilter compositeFilter)
    {
        return compositeFilter.Op switch
        {
            CompositeOperator.And => compositeFilter.Filters?.GetFilterExpressions<TEntity>()?.MergeExpressions(CompositeOperator.And),
            CompositeOperator.Or => compositeFilter.Filters?.GetFilterExpressions<TEntity>()?.MergeExpressions(CompositeOperator.Or),
            _ => throw new QueryBuilderException(new StringBuilder().Append("Invalid composite filter operation - ").Append(compositeFilter.Op).ToString())
        };
    }
    private static Expression<Func<TEntity, bool>> GetFilterExpression<TEntity>(this IFieldFilter fieldFilter)
    {
        var tEntityType = typeof(TEntity);
        var parameter = Expression.Parameter(tEntityType, tEntityType.Name);
        var left = PropertyExpression(parameter, fieldFilter);
        Expression body = fieldFilter.Op switch
        {
            FieldOperator.Equal => Expression.Equal(left, ConstantValueExpression(fieldFilter.Value, left.Type, fieldFilter.Type)),
            FieldOperator.NotEqual => Expression.NotEqual(left, ConstantValueExpression(fieldFilter.Value, left.Type, fieldFilter.Type)),
            FieldOperator.GreaterThan => Expression.GreaterThan(left, ConstantValueExpression(fieldFilter.Value, left.Type, fieldFilter.Type)),
            FieldOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(left, ConstantValueExpression(fieldFilter.Value, left.Type, fieldFilter.Type)),
            FieldOperator.LessThan => Expression.LessThan(left, ConstantValueExpression(fieldFilter.Value, left.Type, fieldFilter.Type)),
            FieldOperator.LessThanOrEqual => Expression.LessThanOrEqual(left, ConstantValueExpression(fieldFilter.Value, left.Type, fieldFilter.Type)),
            FieldOperator.Contains => Expression.Call(left, "Contains", null, ConstantValueExpression(fieldFilter.Value, left.Type, fieldFilter.Type)),
            FieldOperator.StartsWith => Expression.Call(left, "StartsWith", null, ConstantValueExpression(fieldFilter.Value, left.Type, fieldFilter.Type)),
            FieldOperator.EndsWith => Expression.Call(left, "EndsWith", null, ConstantValueExpression(fieldFilter.Value, left.Type, fieldFilter.Type)),
            FieldOperator.In => InOperatorExpresson(left, fieldFilter),
            FieldOperator.NotIn => Expression.Not(InOperatorExpresson(left, fieldFilter)),
            FieldOperator.Between => BetweenOperatorExpression(left, fieldFilter),
            _ => throw new QueryBuilderException(new StringBuilder().Append("Invalid field filter operation - ").Append(fieldFilter.Op).ToString())
        };
        return Expression.Lambda<Func<TEntity, bool>>(body, parameter);
    }
    private static Expression<Func<TEntity, bool>> GetFilterExpression<TEntity>(this IUnaryFilter unaryFilter)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "ufilter");
        var left = PropertyExpression(parameter, unaryFilter);
        Expression body = unaryFilter.Op switch
        {
            UnaryOperator.IsNull => Expression.Equal(left, ConstantValueExpression(null, left.Type)),
            UnaryOperator.IsNotNull => Expression.NotEqual(left, ConstantValueExpression(null, left.Type)),
            _ => throw new QueryBuilderException($"Invalid unary filter operation - {unaryFilter.Op}")
        };
        return Expression.Lambda<Func<TEntity, bool>>(body, parameter);
    }

    #region Composite Filter
    public static IEnumerable<Expression<Func<TEntity, bool>>?> GetFilterExpressions<TEntity>(this IFilter[] filters)
    {
        return filters.Select(x => x.GetFilterExpression<TEntity>());
    }
    public static Expression<Func<TEntity, bool>>? MergeExpressions<TEntity>(this IEnumerable<Expression<Func<TEntity, bool>>?> expressions, CompositeOperator op)
    {
        Expression<Func<TEntity, bool>>? result = expressions.FirstOrDefault();
        if (result != null)
        {
            foreach (var expression in expressions.Skip(1))
            {
                if (expression != null)
                {
                    var invokedExpr = Expression.Invoke(expression, result.Parameters.Cast<Expression>());
                    result = Expression.Lambda<Func<TEntity, bool>>(op switch
                    {
                        CompositeOperator.Or => Expression.OrElse(result.Body, invokedExpr),
                        _ => Expression.AndAlso(result.Body, invokedExpr)
                    }, result.Parameters);
                }
            }
        }
        return result;
    }
    #endregion

    #region Field Filter
    private static MemberExpression PropertyExpression(ParameterExpression parameter, IConditionFilter filter)
    {
        var path = filter.Field.Split(".");
        var property = Expression.Property(parameter, path[0]);
        if (path.Length > 1)
        {
            (property, _) = GetParameterExpression(property, path[1..]);
        }
        return property;
    }
    private static (MemberExpression property, string[] path) GetParameterExpression(MemberExpression property, string[] path)
    {
        return path.Length > 0 ? GetParameterExpression(Expression.Property(property, path[0]), path[1..]) : (property, path);
    }
    private static Expression ConstantValueExpression(object? value, Type type, FilterValueType? valueType = default)
    {
        if (value == null) { return Expression.Constant(null, type); }
        return type switch
        {
            // Other types handling if required 
            { FullName: Names.DateTime } => ToDateTimeExpression(value, valueType),
            { IsEnum: true } => Expression.Convert(Expression.Constant(value), type),
            _ => Expression.Constant(value, type)
        };
    }
    private static Expression ToDateTimeExpression(object value, FilterValueType? valueType)
    {
        valueType ??= FilterValueType.UtcDateTime;
        return valueType switch
        {
            FilterValueType.UtcDateTime => Expressions.ToUniversalTime(value),
            FilterValueType.DateOnly => () => DateOnly.Parse(value.ToString() ?? string.Empty, CultureInfo.InvariantCulture),
            _ => throw new QueryBuilderException(new StringBuilder().Append(valueType).Append(" is not valid type for FilterValueType").ToString())
        };
    }
    private static MethodCallExpression InOperatorExpresson(Expression left, IFieldFilter filter)
    {
        var right = filter.Value;
        if (right is not JArray) throw new ArgumentException("Field Filter value should not be null", nameof(filter));
        return Expression.Call(ContainsMethod.MakeGenericMethod(left.Type), Expression.Constant((right as JArray)?.ToObject(left.Type.MakeArrayType())), left);
    }
    private static BinaryExpression BetweenOperatorExpression(Expression left, IFieldFilter filter)
    {
        var right = filter.Value;
        if (right is not JArray) throw new ArgumentException("Field Filter value cannot be null", nameof(filter));
        var values = (right as JArray)?.ToObject<string[]>();
        if (values == null || values.Length != 2) throw new QueryBuilderException("Field filter values cannot be null and should contain a maximum of 2 values");
        var startValue = ConstantValueExpression(values[0], left.Type, filter.Type);
        var endValue = ConstantValueExpression(values[1], left.Type, filter.Type);
        return Expression.AndAlso(Expression.GreaterThanOrEqual(left, startValue), Expression.LessThanOrEqual(left, endValue));
    }
    #endregion
}
