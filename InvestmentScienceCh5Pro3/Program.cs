using System;
using System.Collections.Generic;
using System.Linq;

namespace InvestmentScienceCh5Pro3
{
	class Program
	{
		/// <summary>The number of projects to consider</summary>
		private const int ProjCount = 7;

		/// <summary>
		/// Project ID is chosen so that it is identified by the bit that it occupies
		/// So first project's ID is:			1
		/// Second project's ID is:		 0b10 = 2
		/// Third project's ID is:		0b100 = 4
		/// etc...
		/// The restriction with this scheme is the fact that an int can have up to 32 bits or a long can have
		/// up to 64 bits.  Beyond that we need to store the projects with an array.
		/// </summary>
		private static readonly List<int> ProjIds = Enumerable.Range(0, ProjCount).Select(p => 1 << p).ToList();

		/// <summary>
		/// Constraints' data
		/// Cost of projects in the first year, the second year and the NPV over the life of the improvement.
		/// Finally the budget per year.
		/// </summary>
		private static readonly List<int> CostYr1 = new List<int> { -90, -80, -50, -20, -40, -80, -80 };
		private static readonly List<int> CostYr2 = new List<int> { -58, -80, -100, -64, -50, -20, -100 };
		private static readonly List<int> Npv = new List<int> { 150, 200, 100, 100, 120, 150, 240 };
		private const int Budget = 250;

		/// <summary>
		/// Total is used to sum up the costs for each of the years and NPV.
		/// </summary>
		/// <param name="xs">list of ProjCount boolean (indicating fund / don't fund)</param>
		/// <param name="cs">Cost of projects</param>
		/// <returns>True: Can be funded, False: Cannot be funded</returns>
		private static readonly Func<IEnumerable<bool>, List<int>, int> Total =
			(xs, cs) => xs.Zip(cs, (x, c) => x ? c : 0).Aggregate(0, (a, c) => a += c);

		/// <summary>The constraints in funding the projects</summary>
		private static readonly List<Func<IEnumerable<bool>, bool>> Constraints = new List<Func<IEnumerable<bool>, bool>> {
			// Constraint 1: First year's total cost must be within budget
			// CostYr1 is negative and hence the negative Total(..)
			xs => -Total(xs, CostYr1) <= Budget,

			// Constraint 2: Second year's total cost must be within budget + last year's left over budget reinvested at
			// 10% interest. so Total cost for year 2
			//		= Budget + 1.1 * (Budget - year1's total)
			//		= 2.1 * Budget - 1.1 * Total(xs, CostYr1)
			// CostYr1 and CostYr2 is negative and hence the negative Total(..)'s
			xs => -Total(xs, CostYr2) <= 2.1 * Budget - 1.1 * (-Total(xs, CostYr1))
		};

		/// <summary>The aggregation of the two constraints</summary>
		private static readonly Func<int, bool> Constraint =
			funded => Constraints.Aggregate(true, (a, con) => a && con(FundedToBoolArray(funded)));

		/// <summary>Boolean array of funded projects, unpacked into a set, IEnumerable</summary>
		/// <param name="x">The numeric id of funded projects</param>
		private static readonly Func<int, IEnumerable<bool>> FundedToBoolArray =
			x => Enumerable.Range(0, ProjCount).Select(i => (x & (1 << i)) == (1 << i));

		private static readonly Stack<TrackingFrame> FundedStack = new Stack<TrackingFrame>();
		private static List<TrackingFrame> _maxNpv = new List<TrackingFrame>();
		private static readonly HashSet<int> Seen = new HashSet<int>();

		private static TrackingFrame MaxNpv(TrackingFrame frame)
		{
			if (_maxNpv.Count == 0)
			{
				_maxNpv.Add(frame);
				return frame;
			}

			var npv = Total(FundedToBoolArray(frame.Funded), Npv);
			var maxNpv = Total(FundedToBoolArray(_maxNpv[0].Funded), Npv);
			if (npv > maxNpv) _maxNpv = new List<TrackingFrame> { frame };
			if (npv == maxNpv) _maxNpv.Add(frame);

			return _maxNpv[0];
		}

		private readonly struct TrackingFrame
		{
			/// <summary>Level within the stack.  This information is not strictly needed, its a nice to have though</summary>
			public int Level { get; }

			/// <summary>The current project index to be added, a value [0..ProjCount)</summary>
			public int ProjInx { get; }

			/// <summary>A numeric representation of funded projects (cumulative).  A value [0..1111111]</summary>
			public int Funded { get; }

			public TrackingFrame(int level, int projInx, int funded) : this()
			{
				Level = level;
				ProjInx = projInx;
				Funded = funded;
			}

			public void Deconstruct(out int level, out int projInx, out int funded)
			{
				level = Level;
				projInx = ProjInx;
				funded = Funded;
			}

			public override string ToString()
			{
				var fundedBits = Convert.ToString(Funded, 2);
				fundedBits = new string(Enumerable.Reverse(fundedBits.ToCharArray()).ToArray())
				             + new string('0', ProjCount - fundedBits.Length);
				var fundedProjects = string.Join(", ", fundedBits.ToCharArray());

				return $"Funded projects: {fundedProjects}"
				       + $"  /  CostYr1: {Total(FundedToBoolArray(Funded), CostYr1),4}"
				       + $"  /  CostYr2: {Total(FundedToBoolArray(Funded), CostYr2),4}"
				       + $"  /  NPV: {Total(FundedToBoolArray(Funded), Npv),4}";
			}
		}

		/// <summary>
		/// Definition:
		///		Project contains:
		///			ProjectID
		///			Cost for Year 1
		///			Cost for Year 2
		///			NPV
		///
		/// Those pieces of data are separated out.  It is possible to lump these pieces of data
		/// into a project class, though I decided to keep the data as it was given.  This code
		/// is small enough that one way of doing it is not significantly superior to another.
		/// The structure I felt will do us better is that of a frame.  A frame contains:
		///			Level	- index in the sack where we will add the next project
		///			ProjInx	- the index of the project that we will add if it meets the constraints
		///			Funded	- All the projects that are funded
		/// 
		/// Algorithm:
		///		Add project to a stack to the point that the last project if added will fail the
		///		constraints. At which point update the projectInx of the last project successfully
		///		added.  When done with all the projects in level n of the stack rewind the stack one
		///		level and repeat the trial of projects.
		///
		///		Pseudo-code of algorithm:
		///		-------------------------
		///		Add first project to a stack (project index projInx) -- frame 0
		///		add project[projInx] to max-npv-list
		///		while
		///			update projInx
		///			if cannot update stack any further then we are done
		///			else if projInx exceeds max projects
		///				unwind stack by one level
		///				set projInx to last frame added projInx
		///			else if adding project[projInx] meets constraints
		///				if projInx was checked before then skip
		///				else
		///					add project[projInx] to stack
		///					update max-npv-list
		///					add all projectInx in the stack to a seen-list
		/// </summary>
		/// <param name="args"></param>
		static void Main(string[] args)
		{
			// Tracking structure.
			TrackingFrame frame = new TrackingFrame();

			// Populate the first frame and stack 0
			var level = 0;
			var firstInx = 0;
			for (var pInx = 0; pInx < ProjCount; ++pInx)
			{
				frame = new TrackingFrame(level, pInx, ProjIds[pInx]);
				if (!Constraint(frame.Funded)) continue;

				FundedStack.Push(frame);
				MaxNpv(frame);
				Seen.Add(frame.Funded);
				firstInx = pInx;
				break;
			}

			// If we could not populate any frame then we cannot fund any project
			if (FundedStack.Count == 0)
			{
				Console.WriteLine("Cannot fund any project");
				return;
			}

			var prX = frame.ProjInx;            // Project Index
			for (; ; )
			{
				++prX;

				// Did we run out of projects to add for this level of the stack
				if (prX >= ProjCount)
				{
					if (FundedStack.Count != 0) FundedStack.Pop();

					// We ran out of frames.  We are done.
					if (FundedStack.Count == 0)
					{
						++firstInx;
						if (firstInx == ProjCount) break;
						frame = new TrackingFrame(level, firstInx, ProjIds[firstInx]);
					}
					else
					{
						frame = FundedStack.Peek();
					}
					prX = frame.ProjInx;
					if (prX == ProjCount) break;
					continue;
				}

				// Add another node to the stack
				var nFr = new TrackingFrame(frame.Level + 1, prX, frame.Funded | ProjIds[prX]);
				if (!Seen.Contains(nFr.Funded) && Constraint(nFr.Funded))
				{
					frame = nFr;
					FundedStack.Push(frame);
					MaxNpv(frame);
					Seen.Add(frame.Funded);
				}
			}

			foreach (var fr in _maxNpv)
				Console.WriteLine(fr.ToString());
		}
	}
}

