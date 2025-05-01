using System.Collections;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Nalu.Cassowary;

namespace Nalu.MagnetLayout;

/// <summary>
/// The magnet stage.
/// </summary>
public class MagnetStage : BindableObject, IMagnetStage, IList<IMagnetElement>
{
    private readonly Solver _solver = new();
    private readonly List<IMagnetElement> _elements = [];
    private IReadOnlyDictionary<string, IMagnetElementBase>? _elementById;

    /// <inheritdoc />
    public string Id => IMagnetStage.StageId;
    
    /// <inheritdoc />
    public Variable Top { get; } = new();

    /// <inheritdoc />
    public Variable Bottom { get; } = new();

    /// <inheritdoc />
    public Variable Left { get; } = new();

    /// <inheritdoc />
    public Variable Right { get; } = new();

    /// <inheritdoc />
    public int Count => _elements.Count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public IMagnetElement this[int index]
    {
        get => _elements[index];
        set
        {
            if (_elements[index] != value)
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                if (value is not null)
                {
                    value.SetStage(this);
                    _elements[index] = value;
                }
                else
                {
                    throw new ArgumentNullException();
                }
            }
        }
    }

    private Constraint _stageStartConstraint;
    private Constraint _stageEndConstraint;
    private Constraint _stageTopConstraint;
    private Constraint _stageBottomConstraint;

    /// <summary>
    /// Initializes a new instance of the <see cref="MagnetStage"/> class.
    /// </summary>
    public MagnetStage()
    {
        Left.SetName("Stage.Start");
        Right.SetName("Stage.End");
        Top.SetName("Stage.Top");
        Bottom.SetName("Stage.Bottom");

        _stageStartConstraint = Left | WeightedRelation.Eq(Strength.Required) | 0;
        _stageEndConstraint = Right | WeightedRelation.Eq(Strength.Required) | 0;
        _stageTopConstraint = Top | WeightedRelation.Eq(Strength.Required) | 0;
        _stageBottomConstraint = Bottom | WeightedRelation.Eq(Strength.Required) | 0;
        
        _solver.AddConstraints(_stageStartConstraint, _stageEndConstraint, _stageTopConstraint, _stageBottomConstraint);
    }

    /// <inheritdoc />
    public void Add(IMagnetElement item)
    {
        _elements.Add(item);
        item.SetStage(this);
    }

    /// <inheritdoc />
    public void Clear()
    {
        foreach (var element in _elements)
        {
            element.SetStage(null);
        }

        _elements.Clear();
    }

    /// <inheritdoc />
    public bool Contains(IMagnetElement item) => _elements.Contains(item);

    /// <inheritdoc />
    public void CopyTo(IMagnetElement[] array, int arrayIndex) => _elements.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public int IndexOf(IMagnetElement item) => _elements.IndexOf(item);

    /// <inheritdoc />
    public void Insert(int index, IMagnetElement item)
    {
        _elements.Insert(index, item);
        item.SetStage(this);
    }

    /// <inheritdoc />
    public bool Remove(IMagnetElement item)
    {
        if (_elements.Remove(item))
        {
            item.SetStage(null);
            _elementById = null;

            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public void RemoveAt(int index)
    {
        var item = _elements[index];
        item.SetStage(null);
        _elements.RemoveAt(index);
        _elementById = null;
    }

    /// <inheritdoc />
    public void Invalidate()
    {
        // No-op for now
    }

    /// <inheritdoc />
    public void AddConstraint(Constraint constraint) => _solver.AddConstraint(constraint);

    /// <inheritdoc />
    public void RemoveConstraint(Constraint constraint) => _solver.RemoveConstraint(constraint);

    /// <inheritdoc />
    public IMagnetElementBase GetElement(string identifier)
    {
        if (identifier == Id)
        {
            return this;
        }

        return GetElementsById()[identifier];
    }

    /// <inheritdoc />
    public bool TryGetElement(string identifier, [NotNullWhen(true)] out IMagnetElementBase? element)
    {
        if (identifier == Id)
        {
            element = this;
            return true;
        }

        return GetElementsById().TryGetValue(identifier, out element);
    }

    /// <inheritdoc />
    public void AddEditVariable(Variable variable, double strength) => _solver.AddEditVariable(variable, strength);

    /// <inheritdoc />
    public void RemoveEditVariable(Variable variable) => _solver.RemoveEditVariable(variable);

    /// <inheritdoc />
    public void SuggestValue(Variable variable, double value) => _solver.SuggestValue(variable, value);
    
    private void SetBounds(double start, double top, double end, double bottom, bool forMeasure)
    {
        // TODO: we having positiveInfinity we have to use LessOrEq
        var endWeightedRelation = forMeasure ? WeightedRelation.LessOrEq(Strength.Required) : WeightedRelation.Eq(Strength.Required);
        var leftConstraint = Left | WeightedRelation.Eq(Strength.Required) | start;
        var rightConstraint = Right | endWeightedRelation | end;
        var topConstraint = Top | WeightedRelation.Eq(Strength.Required) | top;
        var bottomConstraint = Bottom | endWeightedRelation | bottom;

        if (_stageStartConstraint != leftConstraint)
        {
            _solver.RemoveConstraint(_stageStartConstraint);
            _solver.AddConstraint(_stageStartConstraint = leftConstraint);
            Left.CurrentValue = start;
        }
        
        if (_stageEndConstraint != rightConstraint)
        {
            _solver.RemoveConstraint(_stageEndConstraint);
            _solver.AddConstraint(_stageEndConstraint = rightConstraint);
            Right.CurrentValue = end;
        }
        
        if (_stageTopConstraint != topConstraint)
        {
            _solver.RemoveConstraint(_stageTopConstraint);
            _solver.AddConstraint(_stageTopConstraint = topConstraint);
            Top.CurrentValue = top;
        }
        
        if (_stageBottomConstraint != bottomConstraint)
        {
            _solver.RemoveConstraint(_stageBottomConstraint);
            _solver.AddConstraint(_stageBottomConstraint = bottomConstraint);
            Bottom.CurrentValue = bottom;
        }
    }

    /// <inheritdoc />
    public void PrepareForMeasure(double start, double top, double end, double bottom)
    {
        // TODO: optimize depending on Fill or not: when fill we can simply do arrange mode
        SetBounds(start, top, end, bottom, true);

        foreach (var element in _elements)
        {
            element.ApplyConstraints();
        }

        _solver.FetchChanges();

        foreach (var element in _elements)
        {
            element.FinalizeConstraints();
        }

        _solver.FetchChanges();
    }

    /// <inheritdoc />
    public void PrepareForArrange(double start, double top, double end, double bottom)
    {
        SetBounds(start, top, end, bottom, false);

        foreach (var element in _elements)
        {
            element.FinalizeConstraints();
        }

        _solver.FetchChanges();
    }

    /// <inheritdoc />
    public IEnumerator<IMagnetElement> GetEnumerator() => _elements.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IReadOnlyDictionary<string, IMagnetElementBase> GetElementsById() => _elementById ??= _elements.Cast<IMagnetElementBase>().ToFrozenDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);
}
