using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace WalletWasabi.Models
{
	public class CoinsRegistry : ICoinsView
	{
		private HashSet<SmartCoin> Coins { get; }
		private HashSet<SmartCoin> LatestCoinsSnapshot { get; set; }
		private bool InvalidateSnapshot { get; set; }
		private object Lock { get; set; }
		private HashSet<SmartCoin> SpentCoins { get; }
		//		private Dictionary<SmartCoin, List<SmartCoin>> Clusters { get; }

		public CoinsRegistry()
		{
			Coins = new HashSet<SmartCoin>();
			SpentCoins = new HashSet<SmartCoin>();
			//			Clusters = new Dictionary<SmartCoin, List<SmartCoin>>();
			LatestCoinsSnapshot = new HashSet<SmartCoin>();
			InvalidateSnapshot = false;
			Lock = new object();
		}

		private CoinsView AsCoinsView()
		{
			lock (Lock)
			{
				if (InvalidateSnapshot)
				{
					LatestCoinsSnapshot = Coins.ToHashSet(); // Creates a clone
					InvalidateSnapshot = false;
				}
			}
			return new CoinsView(LatestCoinsSnapshot);
		}

		public bool IsEmpty => !AsCoinsView().Any();

		public SmartCoin GetByOutPoint(OutPoint outpoint)
		{
			return AsCoinsView().GetByOutPoint(outpoint);
		}

		public bool TryAdd(SmartCoin coin)
		{
			var added = false;
			lock (Lock)
			{
				added = Coins.Add(coin);
				if (added)
				{
					//					Clusters.Add(coin, new List<SmartCoin>() { coin });
					InvalidateSnapshot = true;
				}
			}
			return added;
		}

		public void Remove(SmartCoin coin)
		{
			var coinsToRemove = AsCoinsView().DescendantOf(coin).ToList();
			coinsToRemove.Add(coin);
			lock (Lock)
			{
				foreach (var toRemove in coinsToRemove)
				{
					if (Coins.Remove(toRemove))
					{
						//Clusters.Remove(toRemove);
					}
				}
				InvalidateSnapshot = true;
			}
		}

		public void Spend(SmartCoin spentCoin)
		{
			lock (Lock)
			{
				if (Coins.Remove(spentCoin))
				{
					SpentCoins.Add(spentCoin);
					var createdCoins = Coins.Where(x => x.TransactionId == spentCoin.SpenderTransactionId);
					foreach (var newCoin in createdCoins)
					{
						spentCoin.Clusters.Merge(newCoin.Clusters);
						newCoin.Clusters = spentCoin.Clusters;
					}
				}
				InvalidateSnapshot = true;
			}
		}

		public ICoinsView AsAllCoinsView()
		{
			lock (Lock)
			{
				return new CoinsView(AsCoinsView().Concat(SpentCoins));
			}
		}

		public ICoinsView AtBlockHeight(Height height)
		{
			return AsCoinsView().AtBlockHeight(height);
		}

		public ICoinsView Available()
		{
			return AsCoinsView().Available();
		}

		public ICoinsView ChildrenOf(SmartCoin coin)
		{
			return AsCoinsView().ChildrenOf(coin);
		}

		public ICoinsView CoinJoinInProcess()
		{
			return AsCoinsView().CoinJoinInProcess();
		}

		public ICoinsView Confirmed()
		{
			return AsCoinsView().Confirmed();
		}

		public ICoinsView DescendantOf(SmartCoin coin)
		{
			return AsCoinsView().DescendantOf(coin);
		}

		public ICoinsView FilterBy(Func<SmartCoin, bool> expression)
		{
			return AsCoinsView().FilterBy(expression);
		}

		public IEnumerator<SmartCoin> GetEnumerator()
		{
			return AsCoinsView().GetEnumerator();
		}

		public ICoinsView OutPoints(IEnumerable<TxoRef> outPoints)
		{
			return AsCoinsView().OutPoints(outPoints);
		}

		public ICoinsView CreatedBy(uint256 txid)
		{
			return AsCoinsView().CreatedBy(txid);
		}

		public ICoinsView SpentBy(uint256 txid)
		{
			return AsCoinsView().SpentBy(txid);
		}

		public SmartCoin[] ToArray()
		{
			return AsCoinsView().ToArray();
		}

		public Money TotalAmount()
		{
			return AsCoinsView().TotalAmount();
		}

		public ICoinsView Unconfirmed()
		{
			return AsCoinsView().Unconfirmed();
		}

		public ICoinsView UnSpent()
		{
			return AsCoinsView().UnSpent();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return AsCoinsView().GetEnumerator();
		}
	}
}
