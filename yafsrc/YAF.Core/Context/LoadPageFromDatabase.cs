// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LoadPageFromDatabase.cs" company="">
//   
// </copyright>
// <summary>
//   The load page from database.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace YAF.Core
{
	using System;
	using System.Data;
	using System.Web;
	using System.Web.Security;

	using YAF.Classes.Data;
	using YAF.Types;
	using YAF.Types.Attributes;
	using YAF.Types.Constants;
	using YAF.Types.EventProxies;
	using YAF.Types.Interfaces;
	using YAF.Types.Interfaces.Extensions;
	using YAF.Utils;
	using YAF.Utils.Extensions;

	/// <summary>
	/// The load page from database.
	/// </summary>
	[ExportService(ServiceLifetimeScope.InstancePerContext, null, typeof(IHandleEvent<InitPageLoadEvent>))]
	public class LoadPageFromDatabase : IHandleEvent<InitPageLoadEvent>, IHaveServiceLocator
	{
		#region Constructors and Destructors

		/// <summary>
		/// Initializes a new instance of the <see cref="LoadPageFromDatabase"/> class.
		/// </summary>
		/// <param name="serviceLocator">
		/// The service locator.
		/// </param>
		/// <param name="legacyDb">
		/// The legacy db.
		/// </param>
		public LoadPageFromDatabase([NotNull] IServiceLocator serviceLocator, ILogger logger)
		{
			this.ServiceLocator = serviceLocator;
			Logger = logger;
		}

		#endregion

		#region Properties

		public ILogger Logger { get; set; }

		/// <summary>
		///   Gets Order.
		/// </summary>
		public int Order
		{
			get
			{
				return 1000;
			}
		}

		/// <summary>
		///   Gets or sets ServiceLocator.
		/// </summary>
		public IServiceLocator ServiceLocator { get; set; }

		#endregion

		#region Implemented Interfaces

		#region IHandleEvent<InitPageLoadEvent>

		/// <summary>
		/// The handle.
		/// </summary>
		/// <param name="event">
		/// The event.
		/// </param>
		/// <exception cref="ApplicationException">Failed to find guest user.</exception>
		public void Handle([NotNull] InitPageLoadEvent @event)
		{
			try
			{
				object userKey = DBNull.Value;

				if (YafContext.Current.User != null)
				{
					userKey = YafContext.Current.User.ProviderUserKey;
				}

				int tries = 0;
				DataRow pageRow;

				do
				{
					pageRow = LegacyDb.pageload(
						this.Get<HttpSessionStateBase>().SessionID,
						YafContext.Current.PageBoardID,
						userKey,
						this.Get<HttpRequestBase>().UserHostAddress,
						this.Get<HttpRequestBase>().FilePath,
						this.Get<HttpRequestBase>().QueryString.ToString(),
						@event.Data.Browser,
						@event.Data.Platform,
						@event.Data.CategoryID,
						@event.Data.ForumID,
						@event.Data.TopicID,
						@event.Data.MessageID,
						// don't track if this is a search engine
						@event.Data.IsSearchEngine,
						@event.Data.IsMobileDevice,
						@event.Data.DontTrack);

					// if the user doesn't exist...
					if (YafContext.Current.User != null && pageRow == null)
					{
						// create the user...
						if (!RoleMembershipHelper.DidCreateForumUser(YafContext.Current.User, YafContext.Current.PageBoardID))
						{
							throw new ApplicationException("Failed to use new user.");
						}
					}

					if (tries++ > 5)
					{
						// fail...
						break;
					}
				}
				while (pageRow == null && YafContext.Current.User != null);

				// page still hasn't been loaded...
				if (pageRow == null)
				{
					throw new ApplicationException("Failed to find guest user.");
				}

				// add all loaded page data into our data dictionary...
				@event.DataDictionary.AddRange(pageRow.ToDictionary());
			}
			catch (Exception x)
			{
#if !DEBUG

				// log the exception...
				this.Logger.Fatal(x, "Failure Initializing User/Page.");

				// log the user out...
				FormsAuthentication.SignOut();

				if (YafContext.Current.ForumPageType != ForumPages.info)
				{
					// show a failure notice since something is probably up with membership...
					YafBuildLink.RedirectInfoPage(InfoMessage.Failure);
				}
				else
				{
					// totally failing... just re-throw the exception...
					throw;
				}
#else
				// re-throw exception...
				throw;
#endif
			}
		}

		#endregion

		#endregion
	}
}