using System;
using ReactiveUI;
using Xamarin.Forms;

namespace ReactiveUI.XamForms
{
    public class ReactiveContentPage<TViewModel> : ContentPage, IViewFor<TViewModel>
        where TViewModel : class
    {
        /// <summary>
        /// The ViewModel to display
        /// </summary>
        public TViewModel ViewModel {
            get { return (TViewModel)GetValue(ViewModelProperty); }
            set { SetValue(ViewModelProperty, value); }
        }

        public static readonly BindableProperty ViewModelProperty = BindableProperty
            .Create(
                nameof(ViewModel),
                typeof(TViewModel),
                typeof(ReactiveContentPage<TViewModel>),
                null,
                BindingMode.OneWay,
                propertyChanged: OnViewModelChanged);
        
        object IViewFor.ViewModel {
            get { return ViewModel; }
            set { ViewModel = (TViewModel)value; }
        }

        private static void OnViewModelChanged(BindableObject bindable, object oldValue, object newValue)
        {
            bindable.BindingContext = newValue;
        }
    }
}