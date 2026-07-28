namespace Skarynagram.Controls;

public partial class LabeledControl : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(
            nameof(Text), 
            typeof(string), 
            typeof(LabeledControl), 
            string.Empty);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public LabeledControl()
    {
        InitializeComponent();
        //lblTitle.BindingContext = this;
        //lblTitle.SetBinding(Label.TextProperty, new Binding(nameof(Text), source: this));
    }
}