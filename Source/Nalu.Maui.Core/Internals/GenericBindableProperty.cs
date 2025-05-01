namespace Nalu.Internals;

internal static class GenericBindableProperty<TBindable>
    where TBindable : BindableObject
{
    public delegate void PropertyChangeDelegate<in TValue>(TValue oldValue, TValue newValue);

    public static BindableProperty Create<TValue>(
        string propertyName,
        TValue defaultValue = default!,
        BindingMode defaultBindingMode = BindingMode.OneWay,
        Func<TBindable, PropertyChangeDelegate<TValue>>? propertyChanged = null,
        Func<TBindable, PropertyChangeDelegate<TValue>>? propertyChanging = null)
        => BindableProperty.Create(
            propertyName,
            typeof(TValue),
            typeof(TBindable),
            defaultValue,
            defaultBindingMode,
            propertyChanged: propertyChanged is not null ? (bindable, value, newValue) => propertyChanged((TBindable)bindable)((TValue)value, (TValue)newValue) : null,
            propertyChanging: propertyChanging is not null ? (bindable, value, newValue) => propertyChanging((TBindable)bindable)((TValue)value, (TValue)newValue) : null);
}
