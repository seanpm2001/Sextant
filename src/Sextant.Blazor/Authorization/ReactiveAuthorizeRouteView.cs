﻿// Copyright (c) 2019 .NET Foundation and Contributors. All rights reserved.
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;

namespace Sextant.Blazor
{
    /// <summary>
    /// Combines the behaviors of <see cref="AuthorizeView"/> and <see cref="ReactiveRouteView"/>,
    /// so that it displays the page matching the specified route but only if the user
    /// is authorized to see it.
    ///
    /// Additionally, this component supplies a cascading parameter of type <see cref="Task{AuthenticationState}"/>,
    /// which makes the user's current authentication state available to descendants.
    /// </summary>
    public class ReactiveAuthorizeRouteView : ReactiveRouteView
    {
        // We expect applications to supply their own authorizing/not-authorized content, but
        // it's better to have defaults than to make the parameters mandatory because in some
        // cases they will never be used (e.g., "authorizing" in out-of-box server-side Blazor)
        private static readonly RenderFragment<AuthenticationState> _defaultNotAuthorizedContent
            = state => builder => builder.AddContent(0, "Not authorized");

        private static readonly RenderFragment _defaultAuthorizingContent
            = builder => builder.AddContent(0, "Authorizing...");

        private readonly RenderFragment _renderAuthorizeRouteViewCoreDelegate;
        private readonly RenderFragment<AuthenticationState> _renderAuthorizedDelegate;
        private readonly RenderFragment<AuthenticationState> _renderNotAuthorizedDelegate;
        private readonly RenderFragment _renderAuthorizingDelegate;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReactiveAuthorizeRouteView"/> class.
        /// </summary>
        public ReactiveAuthorizeRouteView()
        {
            // Cache the rendering delegates so that we only construct new closure instances
            // when they are actually used (e.g., we never prepare a RenderFragment bound to
            // the NotAuthorized content except when you are displaying that particular state)
            RenderFragment renderBaseRouteViewDelegate = base.Render;
            _renderAuthorizedDelegate = authenticateState => renderBaseRouteViewDelegate;
            _renderNotAuthorizedDelegate = authenticationState => builder => RenderNotAuthorizedInDefaultLayout(builder, authenticationState);
            _renderAuthorizingDelegate = RenderAuthorizingInDefaultLayout;
            _renderAuthorizeRouteViewCoreDelegate = RenderAuthorizeRouteViewCore;
        }

        /// <summary>
        /// Gets or sets the content that will be displayed if the user is not authorized.
        /// </summary>
        [Parameter]
        public RenderFragment<AuthenticationState> NotAuthorized { get; set; }

        /// <summary>
        /// Gets or sets the content that will be displayed while asynchronous authorization is in progress.
        /// </summary>
        [Parameter]
        public RenderFragment Authorizing { get; set; }

        [CascadingParameter]
        private Task<AuthenticationState> ExistingCascadedAuthenticationState { get; set; }

        /// <inheritdoc />
        protected override void Render(RenderTreeBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (ExistingCascadedAuthenticationState != null)
            {
                // If this component is already wrapped in a <CascadingAuthenticationState> (or another
                // compatible provider), then don't interfere with the cascaded authentication state.
                _renderAuthorizeRouteViewCoreDelegate(builder);
            }
            else
            {
                // Otherwise, implicitly wrap the output in a <CascadingAuthenticationState>
                builder.OpenComponent<CascadingAuthenticationState>(0);
                builder.AddAttribute(1, nameof(CascadingAuthenticationState.ChildContent), _renderAuthorizeRouteViewCoreDelegate);
                builder.CloseComponent();
            }
        }

        private void RenderAuthorizeRouteViewCore(RenderTreeBuilder builder)
        {
            builder.OpenComponent<AuthorizeRouteViewCore>(0);
            builder.AddAttribute(1, nameof(AuthorizeRouteViewCore.RouteData), RouteData);
            builder.AddAttribute(2, nameof(AuthorizeRouteViewCore.Authorized), _renderAuthorizedDelegate);
            builder.AddAttribute(3, nameof(AuthorizeRouteViewCore.Authorizing), _renderAuthorizingDelegate);
            builder.AddAttribute(4, nameof(AuthorizeRouteViewCore.NotAuthorized), _renderNotAuthorizedDelegate);
            builder.CloseComponent();
        }

        private void RenderContentInDefaultLayout(RenderTreeBuilder builder, RenderFragment content)
        {
            builder.OpenComponent<LayoutView>(0);
            builder.AddAttribute(1, nameof(LayoutView.Layout), DefaultLayout);
            builder.AddAttribute(2, nameof(LayoutView.ChildContent), content);
            builder.CloseComponent();
        }

        private void RenderNotAuthorizedInDefaultLayout(RenderTreeBuilder builder, AuthenticationState authenticationState)
        {
            var content = NotAuthorized ?? _defaultNotAuthorizedContent;
            RenderContentInDefaultLayout(builder, content(authenticationState));
        }

        private void RenderAuthorizingInDefaultLayout(RenderTreeBuilder builder)
        {
            var content = Authorizing ?? _defaultAuthorizingContent;
            RenderContentInDefaultLayout(builder, content);
        }

        private class AuthorizeRouteViewCore : AuthorizeViewCore
        {
            [Parameter]
            public RouteData RouteData { get; set; }

            protected override IAuthorizeData[] GetAuthorizeData()
                => AttributeAuthorizeDataCache.GetAuthorizeDataForType(RouteData.PageType);
        }
    }
}