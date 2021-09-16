using System;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace WalletWasabi.Fluent.Controls
{
	public class TagControl : ContentControl
	{
		private IDisposable? _subscription;
		private TagsBox? _parentTagBox;

		public static readonly StyledProperty<bool> EnableCounterProperty =
			AvaloniaProperty.Register<TagControl, bool>(nameof(EnableCounter));

		public static readonly StyledProperty<bool> EnableDeleteProperty =
			AvaloniaProperty.Register<TagControl, bool>(nameof(EnableDelete));

		public static readonly StyledProperty<int> OrdinalIndexProperty =
			AvaloniaProperty.Register<TagControl, int>(nameof(OrdinalIndex));

		public bool EnableCounter
		{
			get => GetValue(EnableCounterProperty);
			set => SetValue(EnableCounterProperty, value);
		}

		public bool EnableDelete
		{
			get => GetValue(EnableDeleteProperty);
			set => SetValue(EnableDeleteProperty, value);
		}

		public int OrdinalIndex
		{
			get => GetValue(OrdinalIndexProperty);
			set => SetValue(OrdinalIndexProperty, value);
		}

		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);

			_parentTagBox = this.FindLogicalAncestorOfType<TagsBox>();

			var deleteButton = e.NameScope.Find<Button>("PART_DeleteButton");

			if (deleteButton is null)
			{
				return;
			}

			deleteButton.Click += OnDeleteTagClicked;

			_subscription = Disposable.Create(() => deleteButton.Click -= OnDeleteTagClicked);
		}

		protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnDetachedFromVisualTree(e);

			_subscription?.Dispose();
		}

		private void OnDeleteTagClicked(object? sender, RoutedEventArgs e)
		{
			_parentTagBox?.RemoveTargetTag(DataContext);
		}
	}
}
