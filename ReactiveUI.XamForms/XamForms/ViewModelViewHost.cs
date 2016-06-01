using System;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows;
using Splat;
using Xamarin.Forms;

namespace ReactiveUI.XamForms
{
    /// <summary>
    /// This content control will automatically load the View associated with
    /// the ViewModel property and display it. This control is very useful
    /// inside a DataTemplate to display the View associated with a ViewModel.
    /// </summary>
    public class ViewModelViewHost : ContentView, IViewFor, ISupportsManualActivation
    {
        /// <summary>
        /// The ViewModel to display
        /// </summary>
        public object ViewModel {
            get { return GetValue(ViewModelProperty); }
            set { SetValue(ViewModelProperty, value); }
        }
        public static readonly BindableProperty ViewModelProperty = BindableProperty
            .Create(
                nameof(ViewModel),
                typeof(object),
                typeof(ViewModelViewHost),
                null,
                BindingMode.OneWay);

        /// <summary>
        /// If no ViewModel is displayed, this content (i.e. a control) will be displayed.
        /// </summary>
        public View DefaultContent {
            get { return (View)GetValue(DefaultContentProperty); }
            set { SetValue(DefaultContentProperty, value); }
        }
        public static readonly BindableProperty DefaultContentProperty = BindableProperty
            .Create(
                nameof(DefaultContent),
                typeof(View),
                typeof(ViewModelViewHost),
                null,
                BindingMode.OneWay);

        public IObservable<string> ViewContractObservable {
            get { return (IObservable<string>)GetValue(ViewContractObservableProperty); }
            set { SetValue(ViewContractObservableProperty, value); }
        }
        public static readonly BindableProperty ViewContractObservableProperty = BindableProperty
            .Create(
                nameof(ViewContractObservable),
                typeof(IObservable<string>),
                typeof(ViewModelViewHost),
                Observable.Never<string>(),
                BindingMode.OneWay);

        public IViewLocator ViewLocator { get; set; }

        public ViewModelViewHost()
        {
            // NB: InUnitTestRunner also returns true in Design Mode
            if (ModeDetector.InUnitTestRunner()) {
                ViewContractObservable = Observable.Never<string>();
                return;
            }

            //var vmAndContract = Observable.CombineLatest(
            //    this.WhenAnyValue(x => x.ViewModel),
            //    this.WhenAnyObservable(x => x.ViewContractObservable),
            //    (vm, contract) => new { ViewModel = vm, Contract = contract, });

            var platform = Locator.Current.GetService<IPlatformOperations>();
            if (platform == null) {
                throw new Exception("Couldn't find an IPlatformOperations. This should never happen, your dependency resolver is broken");
            }

            ViewContractObservable = Observable.FromEventPattern<EventHandler, EventArgs>(x => SizeChanged += x, x => SizeChanged -= x)
                .Select(_ => platform.GetOrientation())
                .DistinctUntilChanged()
                .StartWith(platform.GetOrientation())
                .Select(x => x != null ? x.ToString() : default(string));

            //(this as IViewFor).WhenActivated(() => {
            //    return new[] { vmAndContract.Subscribe(x => {
            //        if (x.ViewModel == null) {
            //            this.Content = this.DefaultContent;
            //            return;
            //        }

            //        var viewLocator = ViewLocator ?? ReactiveUI.ViewLocator.Current;
            //        var view = viewLocator.ResolveView(x.ViewModel, x.Contract) ?? viewLocator.ResolveView(x.ViewModel, null);

            //        if (view == null) {
            //            throw new Exception(String.Format("Couldn't find view for '{0}'.", x.ViewModel));
            //        }

            //        var castView = view as View;

            //        if (castView == null) {
            //            throw new Exception(String.Format("View '{0}' is not a subclass of '{1}'.", view.GetType().FullName, typeof(View).FullName));
            //        }

            //        view.ViewModel = x.ViewModel;

            //        this.Content = castView;
            //    })};
            //});
        }

        private readonly SerialDisposable currentViewActivation = new SerialDisposable();

        public IDisposable Activate()
        {
            var vmAndContract = Observable.CombineLatest(
                this.WhenAnyValue(x => x.ViewModel),
                this.WhenAnyObservable(x => x.ViewContractObservable).DistinctUntilChanged(),
                (vm, contract) =>
            {
                return new { ViewModel = vm, Contract = contract, };
            });

            var disposables = new CompositeDisposable();

            this
                .currentViewActivation
                .DisposeWith(disposables);

            vmAndContract
                .Subscribe(
                    x =>
                    {
                        if (x.ViewModel == null)
                        {
                            this.Content = this.DefaultContent;
                            return;
                        }

                        var viewLocator = ViewLocator ?? ReactiveUI.ViewLocator.Current;
                        var view = viewLocator.ResolveView(x.ViewModel, x.Contract) ?? viewLocator.ResolveView(x.ViewModel, null);

                        if (view == null)
                        {
                            throw new Exception(String.Format("Couldn't find view for '{0}'.", x.ViewModel));
                        }

                        var castView = view as View;

                        if (castView == null)
                        {
                            throw new Exception(String.Format("View '{0}' is not a subclass of '{1}'.", view.GetType().FullName, typeof(View).FullName));
                        }

                        view.ViewModel = x.ViewModel;

                        var activatableView = castView as ISupportsManualActivation;

                        if (activatableView != null)
                        {
                            this.currentViewActivation.Disposable = activatableView.Activate();
                        }
                        else
                        {
                            this.currentViewActivation.Disposable = Disposable.Empty;
                        }

                        this.Content = castView;
                    })
                .DisposeWith(disposables);

            return disposables;
        }
    }

    public interface ISupportsManualActivation
    {
        IDisposable Activate();
    }
}
