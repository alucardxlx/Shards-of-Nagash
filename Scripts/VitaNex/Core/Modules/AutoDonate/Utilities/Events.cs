#region Header
//   Vorspire    _,-'/-'/  Events.cs
//   .      __,-; ,'( '/
//    \.    `-.__`-._`:_,-._       _ , . ``
//     `:-._,------' ` _,`--` -: `_ , ` ,' :
//        `---..__,,--'  (C) 2016  ` -'. -'
//        #  Vita-Nex [http://core.vita-nex.com]  #
//  {o)xxx|===============-   #   -===============|xxx(o}
//        #        The MIT License (MIT)          #
#endregion

#region References
using System;

using Server.Items;
#endregion

namespace VitaNex.Modules.AutoDonate
{
	public static class DonationEvents
	{
		#region TransVoided
		public delegate void TransVoided(TransVoidedEventArgs e);

		public static event TransVoided OnTransVoided;

		public static void InvokeTransVoided(DonationTransaction trans)
		{
			if (OnTransVoided != null)
			{
				OnTransVoided(new TransVoidedEventArgs(trans));
			}
		}

		public sealed class TransVoidedEventArgs : EventArgs
		{
			public DonationTransaction Transaction { get; private set; }

			public TransVoidedEventArgs(DonationTransaction trans)
			{
				Transaction = trans;
			}
		}
		#endregion TransVoided

		#region TransClaimed
		public delegate void TransClaimed(TransClaimedEventArgs e);

		public static event TransClaimed OnTransClaimed;

		public static void InvokeTransClaimed(DonationTransaction trans)
		{
			if (OnTransClaimed != null)
			{
				OnTransClaimed(new TransClaimedEventArgs(trans));
			}
		}

		public sealed class TransClaimedEventArgs : EventArgs
		{
			public DonationTransaction Transaction { get; private set; }

			public TransClaimedEventArgs(DonationTransaction trans)
			{
				Transaction = trans;
			}
		}
		#endregion TransClaimed

		#region TransProcessed
		public delegate void TransProcessed(TransProcessedEventArgs e);

		public static event TransProcessed OnTransProcessed;

		public static void InvokeTransProcessed(DonationTransaction trans)
		{
			if (OnTransProcessed != null)
			{
				OnTransProcessed(new TransProcessedEventArgs(trans));
			}
		}

		public sealed class TransProcessedEventArgs : EventArgs
		{
			public DonationTransaction Transaction { get; private set; }

			public TransProcessedEventArgs(DonationTransaction trans)
			{
				Transaction = trans;
			}
		}
		#endregion TransProcessed

		#region StateChanged
		public delegate void StateChanged(StateChangedEventArgs e);

		public static event StateChanged OnStateChanged;

		public static void InvokeStateChanged(DonationTransaction trans, TransactionState oldState)
		{
			if (OnStateChanged != null)
			{
				OnStateChanged(new StateChangedEventArgs(trans, oldState));
			}
		}

		public sealed class StateChangedEventArgs : EventArgs
		{
			public DonationTransaction Transaction { get; private set; }
			public TransactionState OldState { get; private set; }

			public StateChangedEventArgs(DonationTransaction trans, TransactionState oldState)
			{
				Transaction = trans;
				OldState = oldState;
			}
		}
		#endregion StateChanged

		#region TransactionExchange
		public delegate void TransExchanger(TransExchangeEventArgs e);

		public static event TransExchanger OnTransExchange;

		public static long InvokeTransExchange(DonationTransaction trans, DonationProfile dp)
		{
			var e = new TransExchangeEventArgs(trans, dp);

			if (OnTransExchange != null)
			{
				OnTransExchange(e);
			}

			return e.Exchanged;
		}

		public sealed class TransExchangeEventArgs : EventArgs
		{
			public DonationTransaction Transaction { get; private set; }
			public DonationProfile Profile { get; private set; }

			public long Exchanged { get; set; }

			public ulong Flags { get; set; }

			public TransExchangeEventArgs(DonationTransaction trans, DonationProfile dp)
			{
				Transaction = trans;
				Profile = dp;

				Exchanged = Transaction.Credit;
			}
		}
		#endregion

		#region TransactionPack
		public delegate void TransPacker(TransPackEventArgs e);

		public static event TransPacker OnTransPack;

		public static Container InvokeTransPack(DonationTransaction trans, DonationProfile dp)
		{
			var cont = new Bag
			{
				Name = "A Donation Reward Bag",
				Hue = 1152
			};

			var e = new TransPackEventArgs(trans, dp, cont);

			if (OnTransPack != null)
			{
				OnTransPack(e);
			}

			return e.Container;
		}

		public sealed class TransPackEventArgs : EventArgs
		{
			public DonationTransaction Transaction { get; private set; }
			public DonationProfile Profile { get; private set; }

			public Container Container { get; set; }

			public ulong Flags { get; set; }

			public TransPackEventArgs(DonationTransaction trans, DonationProfile dp, Container cont)
			{
				Transaction = trans;
				Profile = dp;

				Container = cont;
			}
		}
		#endregion
	}
}