using System;
using System.Reactive.Linq;
using ReactiveUI;
using System.Collections.Specialized;

#if UNIFIED && UIKIT
using UIKit;
using NSView = UIKit.UIView;
using NSViewController = UIKit.UIViewController;
#elif UNIFIED && COCOA
using AppKit;
#elif UIKIT
using MonoTouch.UIKit;
using NSView = MonoTouch.UIKit.UIView;
using NSViewController = MonoTouch.UIKit.UIViewController;
#else
using MonoMac.AppKit;
#endif

namespace ReactiveUI
{
    public class RoutedViewHost : ReactiveNavigationController
    {
        private RoutingState router;
        private IObservable<string> viewContractObservable;
        private IViewLocator viewLocator;
        private bool popIsRouterInstigated;

        public RoutingState Router
        {
            get { return router; }
            set { this.RaiseAndSetIfChanged(ref router, value); }
        }

        public IObservable<string> ViewContractObservable
        {
            get { return viewContractObservable; }
            set { this.RaiseAndSetIfChanged(ref viewContractObservable, value); }
        }

        public IViewLocator ViewLocator
        {
            get { return this.viewLocator; }
            set { this.viewLocator = value; }
        }

        public RoutedViewHost()
        {
            this.WhenActivated(
                d =>
                {
                    d(this
                        .WhenAnyObservable(x => x.Router.NavigationStack.Changed)
                        .Where(x => x.Action == NotifyCollectionChangedAction.Reset)
                        .Subscribe(_ =>
                        {
                            this.popIsRouterInstigated = true;
                            this.PopToRootViewController(true);
                            this.popIsRouterInstigated = false;
                        }));

                    d(this
                        .WhenAnyObservable(x => x.Router.Navigate)
                        .CombineLatest(this.WhenAnyObservable(x => x.ViewContractObservable), (_, contract) => contract)
                        .Select(contract => this.ResolveView(this.Router.GetCurrentViewModel(), contract))
                        .Subscribe(x => this.PushViewController(x, true)));

                    d(this
                        .WhenAnyObservable(x => x.Router.NavigateBack)
                        .Subscribe(x =>
                        {
                            this.popIsRouterInstigated = true;
                            this.PopViewController(true);
                            this.popIsRouterInstigated = false;
                        }));
                });
        }

        public override NSViewController PopViewController(bool animated)
        {
            if (!this.popIsRouterInstigated)
            {
                // user must have clicked Back button in nav controller, so we need to manually sync up the router state
                this.Router.NavigationStack.RemoveAt(this.router.NavigationStack.Count - 1);
            }

            return base.PopViewController(animated);
        }

        private UIViewController ResolveView(IRoutableViewModel viewModel, string contract)
        {
            if (viewModel == null)
            {
                return null;
            }

            var viewLocator = this.ViewLocator ?? ReactiveUI.ViewLocator.Current;
            var view = viewLocator.ResolveView(viewModel, contract);

            if (view == null)
            {
                throw new Exception(
                    string.Format(
                        "Couldn't find a view for view model. You probably need to register an IViewFor<{0}>",
                        viewModel.GetType().Name));
            }

            view.ViewModel = viewModel;
            var viewController = view as UIViewController;

            if (viewController == null)
            {
                throw new Exception(
                    string.Format(
                        "View type {0} for view model type {1} is not a UIViewController",
                        view.GetType().Name,
                        viewModel.GetType().Name));
            }

            viewController.NavigationItem.Title = viewModel.UrlPathSegment;
            return viewController;
        }
    }

    /// <summary>
    /// RoutedViewHost is a helper class that will connect a RoutingState
    /// to an arbitrary NSView and attempt to load the View for the latest
    /// ViewModel as a child view of the target. Usually the target view will
    /// be the NSWindow.
    /// 
    /// This is a bit different than the XAML's RoutedViewHost in the sense
    /// that this isn't a Control itself, it only manipulates other Views.
    /// </summary>
    [Obsolete("Use RoutedViewHost instead. This class will be removed in a later release.")]
    public class RoutedViewHostLegacy : ReactiveObject
    {
        RoutingState _Router;
        public RoutingState Router {
            get { return _Router; }
            set { this.RaiseAndSetIfChanged(ref _Router, value); }
        }

        IObservable<string> _ViewContractObservable;
        public IObservable<string> ViewContractObservable {
            get { return _ViewContractObservable; }
            set { this.RaiseAndSetIfChanged(ref _ViewContractObservable, value); }
        }
        
        NSViewController _DefaultContent;
        public NSViewController DefaultContent {
            get { return _DefaultContent; }
            set { this.RaiseAndSetIfChanged(ref _DefaultContent, value); }
        }
        
        public IViewLocator ViewLocator { get; set; }

        public RoutedViewHostLegacy(NSView targetView)
        {
            NSView viewLastAdded = null;

            ViewContractObservable = Observable.Return(default(string));
                        
            var vmAndContract = Observable.CombineLatest(
                this.WhenAnyObservable(x => x.Router.CurrentViewModel),
                this.WhenAnyObservable(x => x.ViewContractObservable),
                (vm, contract) => new { ViewModel = vm, Contract = contract, });

            vmAndContract.Subscribe(x => {
                if (viewLastAdded != null)
                    viewLastAdded.RemoveFromSuperview();

                if (x.ViewModel == null) {
                    if (DefaultContent != null)
                        targetView.AddSubview(DefaultContent.View);
                    return;
                }

                var viewLocator = ViewLocator ?? ReactiveUI.ViewLocator.Current;
                var view = viewLocator.ResolveView(x.ViewModel, x.Contract) ?? viewLocator.ResolveView(x.ViewModel, null);
                view.ViewModel = x.ViewModel;

                if (view is NSViewController) {
                    viewLastAdded = ((NSViewController)view).View;
                } else if (view is NSView) {
                    viewLastAdded = (NSView)view;
                } else {
                    throw new Exception(String.Format("'{0}' must be an NSViewController or NSView", view.GetType().FullName));
                }

                targetView.AddSubview(viewLastAdded);           
            }, RxApp.DefaultExceptionHandler.OnNext);
        }
    }
}
