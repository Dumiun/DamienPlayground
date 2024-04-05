using DamienPlaygroundLibrary.Models;
using DamienPlaygroundLibrary.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MudBlazor;
using System.Linq;
using System.Threading;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DamienPlayground.Data
{
	public class BackgroundWorkerService : BackgroundService
	{
		private readonly ILogger<BackgroundWorkerService> _logger;
		private readonly IDataAccessService _dataAccess;

		public BackgroundWorkerService(ILogger<BackgroundWorkerService> logger, IDataAccessService dataAccess)
		{
			_logger = logger;
			_dataAccess = dataAccess;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				List<List<Lib_BettingFormIndividualRowEntry>> Entries = new List<List<Lib_BettingFormIndividualRowEntry>>();
				DateTime now = DateTime.Now;

				//   if ((now.DayOfWeek == DayOfWeek.Wednesday || now.DayOfWeek == DayOfWeek.Saturday) && now.Hour == 15 && now.Minute == 0 && now.Second == 0)
				if ((now.DayOfWeek == DayOfWeek.Wednesday) && now.Hour == 15 && now.Minute == 00 && now.Second == 0)
				{
					var listOfInvoices = await _dataAccess.LoadData<string, dynamic>("dbo.spGetAllWeekdayFixedDateInvoices_Search", new { }, "DefaultConnection");

					foreach (var invoices in listOfInvoices)
					{
						var level1_details = await _dataAccess.LoadData<FixedDate_Level1DataModel, dynamic>("dbo.spGetFixedDateLevel1Details_Search", new { InvoiceId = invoices }, "DefaultConnection");
						var entry = await _dataAccess.LoadData<Lib_BettingFormIndividualRowEntry, dynamic>("dbo.spGetAllWeekdayFixedDateEntries", new { InvoiceId = invoices }, "DefaultConnection");
						foreach (var e in entry)
						{
							e.DrawDates = GetDrawDatesForEntry(int.Parse(e.Day));
							List<int> variations = CalculateVariations(e.Number, e.Roll);
							e.NumberVariations = variations.Select(x => x.ToString()).ToList();
							if (e.Roll != "I")
							{
								e.ActualBig = e.Big * e.NumberVariations.Count * e.DrawDates.Count;
								e.ActualBigPerDrawDate = e.Big * e.NumberVariations.Count;
								e.ActualSmall = e.Small * e.NumberVariations.Count * e.DrawDates.Count;
								e.ActualSmallPerDrawDate = e.Small * e.NumberVariations.Count;
							}
							if (e.Roll == "I")
							{
								e.ActualBig = e.Big * e.DrawDates.Count;
								e.ActualBigPerDrawDate = e.Big;
								e.ActualSmall = e.Small * e.DrawDates.Count;
								e.ActualSmallPerDrawDate = e.Small;
							}
							e.BigCostForEntry = e.ActualBig * 1.6;
							e.SmallCostForEntry = e.ActualSmall * 0.7;
							e.TotalCostForEntry = e.BigCostForEntry + e.SmallCostForEntry;
							e.TotalCostForEntryPerDrawDate = (e.ActualBigPerDrawDate * 1.6) + (e.ActualSmallPerDrawDate * 0.7);
						}
						string dayAbbreviation = DateTime.Now.ToString("ddd").ToUpper();
						string dateFormatted = DateTime.Now.ToString("yyyyMMdd");
						Random generator = new Random();
						string r = generator.Next(0, 1000000).ToString("D6");
						string InvoiceId = $"{dayAbbreviation}-{dateFormatted}-{r}-{"FixedDate"}";

						try
						{
							List<DateTime> uniqueDrawDates = new List<DateTime>();
							for (int i = 0; i < entry.Count; i++)
							{
								if (entry[i].DrawDates != null)
								{
									foreach (var drawdate in entry[i].DrawDates)
									{
										//Level 5 data entry
										for (int x = 0; x < entry[i].NumberVariations.Count; x++)
										{
											// Check if any of the required values are empty or zero
											if (!string.IsNullOrEmpty(entry[i].NumberVariations[x].ToString()))
											{
												ILib_InvoiceLevel5DataModel level5entry = new Lib_InvoiceLevel5DataModel();
												level5entry.InvoiceId = InvoiceId;
												level5entry.NumberVariation = entry[i].NumberVariations[x];
												level5entry.PurchasedNumber = entry[i].Number;
												level5entry.PurchasedRoll = entry[i].Roll;
												level5entry.Big = entry[i].Big;
												level5entry.Small = entry[i].Small;
												level5entry.StrikeAmount = 0.00;
												level5entry.DrawDate = drawdate;

												var mapped5 = new
												{
													level5entry.InvoiceId,
													level5entry.NumberVariation,
													level5entry.PurchasedNumber,
													level5entry.PurchasedRoll,
													level5entry.Big,
													level5entry.Small,
													level5entry.DrawDate,
													level5entry.StrikeAmount
												};
												await _dataAccess.SaveData("dbo.sp4DInvoice_Level5_Insert", mapped5, "DefaultConnection");
											}
										}
										// Check if any of the required values are empty or zero
										if (!string.IsNullOrEmpty(entry[i].Number))
										{
											//level 4 data entry
											ILib_InvoiceLevel4DataModel level4entry = new Lib_InvoiceLevel4DataModel();
											level4entry.InvoiceId = InvoiceId;
											level4entry.Number = entry[i].Number;
											level4entry.Big = entry[i].Big;
											level4entry.Small = entry[i].Small;
											level4entry.Roll = entry[i].Roll;
											level4entry.DrawDate = drawdate;

											var mapped4 = new
											{
												level4entry.InvoiceId,
												level4entry.Number,
												level4entry.Big,
												level4entry.Small,
												level4entry.Roll,
												DrawDate = level4entry.DrawDate
											};

											await _dataAccess.SaveData("dbo.sp4DInvoice_Level4_Insert", mapped4, "DefaultConnection");
										}

										uniqueDrawDates.Add(drawdate);
									}
									if (!string.IsNullOrEmpty(entry[i].Number))
									{
										//level 3 data entry
										ILib_InvoiceLevel3DataModel level3entry = new Lib_InvoiceLevel3DataModel();
										level3entry.DrawDates = entry[i].DrawDates;
										level3entry.InvoiceId = InvoiceId;
										level3entry.Number = entry[i].Number;
										level3entry.NoOfVariations = entry[i].NumberVariations.Count;
										level3entry.Big = entry[i].Big;
										level3entry.Small = entry[i].Small;
										level3entry.Roll = entry[i].Roll;
										level3entry.Day = entry[i].Day;
										level3entry.ActualBig = entry[i].ActualBig;
										level3entry.ActualSmall = entry[i].ActualSmall;
										level3entry.TotalCostForEntry = entry[i].TotalCostForEntry;

										var mapped3 = new
										{
											level3entry.InvoiceId,
											level3entry.Number,
											level3entry.NoOfVariations,
											level3entry.Big,
											level3entry.Small,
											level3entry.Day,
											level3entry.Roll,
											level3entry.ActualBig,
											level3entry.ActualSmall,
											level3entry.TotalCostForEntry,
											DrawDate1 = level3entry.DrawDates.Count > 0 ? level3entry.DrawDates[0] : (DateTime?)null,
											DrawDate2 = level3entry.DrawDates.Count > 1 ? level3entry.DrawDates[1] : (DateTime?)null,
											DrawDate3 = level3entry.DrawDates.Count > 2 ? level3entry.DrawDates[2] : (DateTime?)null
										};

										await _dataAccess.SaveData("dbo.sp4DInvoice_Level3_Insert", mapped3, "DefaultConnection");
									}
								}
							}

							//level 1 data entry
							ILib_InvoiceLevel1DataModel level1entry = new Lib_InvoiceLevel1DataModel();
							level1entry.InvoiceId = InvoiceId;
							level1entry.TotalBig = level1_details.FirstOrDefault().TotalBig;
							level1entry.TotalSmall = level1_details.FirstOrDefault().TotalSmall;
							level1entry.TotalAmount = level1_details.FirstOrDefault().TotalAmount;
							level1entry.StrikeAmount = 0.00;
							level1entry.PurchasedForId = level1_details.FirstOrDefault().PurchasedForId;
							level1entry.PurchasedById = level1_details.FirstOrDefault().PurchasedById;
							level1entry.MediaType = "Fixed Date";
							level1entry.PurchasedDate = DateTime.Now;
							level1entry.LastUpdatedOn = DateTime.Now;
							level1entry.PageName = level1_details.FirstOrDefault().PageName;

							var mapped1 = new
							{
								level1entry.InvoiceId,
								level1entry.PageName,
								level1entry.TotalBig,
								level1entry.TotalSmall,
								level1entry.CommsPercentage,
								level1entry.TotalAmount,
								level1entry.StrikeAmount,
								level1entry.PurchasedById,
								level1entry.PurchasedForId,
								level1entry.MediaType,
								level1entry.PurchasedDate,
								level1entry.LastUpdatedOn
							};

							await _dataAccess.SaveData("dbo.sp4DInvoice_Level1_Insert", mapped1, "DefaultConnection");

							//filter the unique draw dates so that all the entries in the List is distinct
							uniqueDrawDates = uniqueDrawDates.Distinct().ToList();

							foreach (var drawDates in uniqueDrawDates)
							{
								Lib_InvoiceLevel2DataModel level2entry = new Lib_InvoiceLevel2DataModel();

								level2entry.DrawDate = drawDates;
								level2entry.InvoiceId = InvoiceId;
								level2entry.PageName = "Fixed Date";

								foreach (var i in entry)
								{
									if (i.DrawDates != null)
									{
										// if i.DrawDates contains the drawDates, then add the TotalBig and TotalSmall to the level15entry. Only compare the Date NOT the Time
										if (i.DrawDates.Contains(drawDates.Date))
										{
											level2entry.TotalBig += i.ActualBigPerDrawDate;
											level2entry.TotalSmall += i.ActualSmallPerDrawDate;
											level2entry.TotalAmount += i.TotalCostForEntryPerDrawDate;
										}
									}
								}

								level2entry.StrikeAmount = 0.00;
								level2entry.PurchasedForId = level1_details.FirstOrDefault().PurchasedForId;
								level2entry.PurchasedById = level1_details.FirstOrDefault().PurchasedById;
								level2entry.PurchasedDate = DateTime.Now;
								level2entry.LastUpdatedOn = DateTime.Now;

								var mapped2 = new
								{
									level2entry.InvoiceId,
									level2entry.PageName,
									level2entry.DrawDate,
									level2entry.TotalBig,
									level2entry.TotalSmall,
									level2entry.CommsPercentage,
									level2entry.TotalAmount,
									level2entry.StrikeAmount,
									level2entry.PurchasedById,
									level2entry.PurchasedForId,
									level2entry.PurchasedDate,
									level2entry.LastUpdatedOn
								};
								await _dataAccess.SaveData("dbo.sp4DInvoice_Level2_Insert", mapped2, "DefaultConnection");
							}
						}
						catch (Exception ex)
						{
							Console.WriteLine(ex.Message);
						}
					}
				}

				if ((now.DayOfWeek == DayOfWeek.Saturday) && now.Hour == 15 && now.Minute == 00 && now.Second == 0)
				{
					var listOfInvoices = await _dataAccess.LoadData<string, dynamic>("dbo.spGetAllWeekenedFixedDateInvoices_Search", new { }, "DefaultConnection");

					foreach (var invoices in listOfInvoices)
					{
						var level1_details = await _dataAccess.LoadData<FixedDate_Level1DataModel, dynamic>("dbo.spGetFixedDateLevel1Details_Search", new { InvoiceId = invoices }, "DefaultConnection");
						var entry = await _dataAccess.LoadData<Lib_BettingFormIndividualRowEntry, dynamic>("dbo.spGetAllWeekendFixedDateEntries", new { InvoiceId = invoices }, "DefaultConnection");
						foreach (var e in entry)
						{
							e.DrawDates = GetDrawDatesForEntry(int.Parse(e.Day));
							List<int> variations = CalculateVariations(e.Number, e.Roll);
							e.NumberVariations = variations.Select(x => x.ToString()).ToList();
							if (e.Roll != "I")
							{
								e.ActualBig = e.Big * e.NumberVariations.Count * e.DrawDates.Count;
								e.ActualBigPerDrawDate = e.Big * e.NumberVariations.Count;
								e.ActualSmall = e.Small * e.NumberVariations.Count * e.DrawDates.Count;
								e.ActualSmallPerDrawDate = e.Small * e.NumberVariations.Count;
							}
							if (e.Roll == "I")
							{
								e.ActualBig = e.Big * e.DrawDates.Count;
								e.ActualBigPerDrawDate = e.Big;
								e.ActualSmall = e.Small * e.DrawDates.Count;
								e.ActualSmallPerDrawDate = e.Small;
							}
							e.BigCostForEntry = e.ActualBig * 1.6;
							e.SmallCostForEntry = e.ActualSmall * 0.7;
							e.TotalCostForEntry = e.BigCostForEntry + e.SmallCostForEntry;
							e.TotalCostForEntryPerDrawDate = (e.ActualBigPerDrawDate * 1.6) + (e.ActualSmallPerDrawDate * 0.7);
						}
						string dayAbbreviation = DateTime.Now.ToString("ddd").ToUpper();
						string dateFormatted = DateTime.Now.ToString("yyyyMMdd");
						Random generator = new Random();
						string r = generator.Next(0, 1000000).ToString("D6");
						string InvoiceId = $"{dayAbbreviation}-{dateFormatted}-{r}-{"FixedDate"}";

						try
						{
							List<DateTime> uniqueDrawDates = new List<DateTime>();
							for (int i = 0; i < entry.Count; i++)
							{
								if (entry[i].DrawDates != null)
								{
									foreach (var drawdate in entry[i].DrawDates)
									{
										//Level 5 data entry
										for (int x = 0; x < entry[i].NumberVariations.Count; x++)
										{
											// Check if any of the required values are empty or zero
											if (!string.IsNullOrEmpty(entry[i].NumberVariations[x].ToString()))
											{
												ILib_InvoiceLevel5DataModel level5entry = new Lib_InvoiceLevel5DataModel();
												level5entry.InvoiceId = InvoiceId;
												level5entry.NumberVariation = entry[i].NumberVariations[x];
												level5entry.PurchasedNumber = entry[i].Number;
												level5entry.PurchasedRoll = entry[i].Roll;
												level5entry.Big = entry[i].Big;
												level5entry.Small = entry[i].Small;
												level5entry.StrikeAmount = 0.00;
												level5entry.DrawDate = drawdate;

												var mapped5 = new
												{
													level5entry.InvoiceId,
													level5entry.NumberVariation,
													level5entry.PurchasedNumber,
													level5entry.PurchasedRoll,
													level5entry.Big,
													level5entry.Small,
													level5entry.DrawDate,
													level5entry.StrikeAmount
												};
												await _dataAccess.SaveData("dbo.sp4DInvoice_Level5_Insert", mapped5, "DefaultConnection");
											}
										}
										// Check if any of the required values are empty or zero
										if (!string.IsNullOrEmpty(entry[i].Number))
										{
											//level 4 data entry
											ILib_InvoiceLevel4DataModel level4entry = new Lib_InvoiceLevel4DataModel();
											level4entry.InvoiceId = InvoiceId;
											level4entry.Number = entry[i].Number;
											level4entry.Big = entry[i].Big;
											level4entry.Small = entry[i].Small;
											level4entry.Roll = entry[i].Roll;
											level4entry.DrawDate = drawdate;

											var mapped4 = new
											{
												level4entry.InvoiceId,
												level4entry.Number,
												level4entry.Big,
												level4entry.Small,
												level4entry.Roll,
												DrawDate = level4entry.DrawDate
											};

											await _dataAccess.SaveData("dbo.sp4DInvoice_Level4_Insert", mapped4, "DefaultConnection");
										}

										uniqueDrawDates.Add(drawdate);
									}
									if (!string.IsNullOrEmpty(entry[i].Number))
									{
										//level 3 data entry
										ILib_InvoiceLevel3DataModel level3entry = new Lib_InvoiceLevel3DataModel();
										level3entry.DrawDates = entry[i].DrawDates;
										level3entry.InvoiceId = InvoiceId;
										level3entry.Number = entry[i].Number;
										level3entry.NoOfVariations = entry[i].NumberVariations.Count;
										level3entry.Big = entry[i].Big;
										level3entry.Small = entry[i].Small;
										level3entry.Roll = entry[i].Roll;
										level3entry.Day = entry[i].Day;
										level3entry.ActualBig = entry[i].ActualBig;
										level3entry.ActualSmall = entry[i].ActualSmall;
										level3entry.TotalCostForEntry = entry[i].TotalCostForEntry;

										var mapped3 = new
										{
											level3entry.InvoiceId,
											level3entry.Number,
											level3entry.NoOfVariations,
											level3entry.Big,
											level3entry.Small,
											level3entry.Day,
											level3entry.Roll,
											level3entry.ActualBig,
											level3entry.ActualSmall,
											level3entry.TotalCostForEntry,
											DrawDate1 = level3entry.DrawDates.Count > 0 ? level3entry.DrawDates[0] : (DateTime?)null,
											DrawDate2 = level3entry.DrawDates.Count > 1 ? level3entry.DrawDates[1] : (DateTime?)null,
											DrawDate3 = level3entry.DrawDates.Count > 2 ? level3entry.DrawDates[2] : (DateTime?)null
										};

										await _dataAccess.SaveData("dbo.sp4DInvoice_Level3_Insert", mapped3, "DefaultConnection");
									}
								}
							}

							//level 1 data entry
							ILib_InvoiceLevel1DataModel level1entry = new Lib_InvoiceLevel1DataModel();
							level1entry.InvoiceId = InvoiceId;
							level1entry.TotalBig = level1_details.FirstOrDefault().TotalBig;
							level1entry.TotalSmall = level1_details.FirstOrDefault().TotalSmall;
							level1entry.TotalAmount = level1_details.FirstOrDefault().TotalAmount;
							level1entry.StrikeAmount = 0.00;
							level1entry.PurchasedForId = level1_details.FirstOrDefault().PurchasedForId;
							level1entry.PurchasedById = level1_details.FirstOrDefault().PurchasedById;
							level1entry.MediaType = "Fixed Date";
							level1entry.PurchasedDate = DateTime.Now;
							level1entry.LastUpdatedOn = DateTime.Now;
							level1entry.PageName = level1_details.FirstOrDefault().PageName;

							var mapped1 = new
							{
								level1entry.InvoiceId,
								level1entry.PageName,
								level1entry.TotalBig,
								level1entry.TotalSmall,
								level1entry.CommsPercentage,
								level1entry.TotalAmount,
								level1entry.StrikeAmount,
								level1entry.PurchasedById,
								level1entry.PurchasedForId,
								level1entry.MediaType,
								level1entry.PurchasedDate,
								level1entry.LastUpdatedOn
							};

							await _dataAccess.SaveData("dbo.sp4DInvoice_Level1_Insert", mapped1, "DefaultConnection");

							//filter the unique draw dates so that all the entries in the List is distinct
							uniqueDrawDates = uniqueDrawDates.Distinct().ToList();

							foreach (var drawDates in uniqueDrawDates)
							{
								Lib_InvoiceLevel2DataModel level2entry = new Lib_InvoiceLevel2DataModel();

								level2entry.DrawDate = drawDates;
								level2entry.InvoiceId = InvoiceId;
								level2entry.PageName = "Fixed Date";

								foreach (var i in entry)
								{
									if (i.DrawDates != null)
									{
										// if i.DrawDates contains the drawDates, then add the TotalBig and TotalSmall to the level15entry. Only compare the Date NOT the Time
										if (i.DrawDates.Contains(drawDates.Date))
										{
											level2entry.TotalBig += i.ActualBigPerDrawDate;
											level2entry.TotalSmall += i.ActualSmallPerDrawDate;
											level2entry.TotalAmount += i.TotalCostForEntryPerDrawDate;
										}
									}
								}

								level2entry.StrikeAmount = 0.00;
								level2entry.PurchasedForId = level1_details.FirstOrDefault().PurchasedForId;
								level2entry.PurchasedById = level1_details.FirstOrDefault().PurchasedById;
								level2entry.PurchasedDate = DateTime.Now;
								level2entry.LastUpdatedOn = DateTime.Now;

								var mapped2 = new
								{
									level2entry.InvoiceId,
									level2entry.PageName,
									level2entry.DrawDate,
									level2entry.TotalBig,
									level2entry.TotalSmall,
									level2entry.CommsPercentage,
									level2entry.TotalAmount,
									level2entry.StrikeAmount,
									level2entry.PurchasedById,
									level2entry.PurchasedForId,
									level2entry.PurchasedDate,
									level2entry.LastUpdatedOn
								};
								await _dataAccess.SaveData("dbo.sp4DInvoice_Level2_Insert", mapped2, "DefaultConnection");
							}
						}
						catch (Exception ex)
						{
							Console.WriteLine(ex.Message);
						}
					}
				}

				if ((now.DayOfWeek == DayOfWeek.Monday) && now.Hour == 1 && now.Minute == 0 && now.Second == 0)
				{
					await _dataAccess.SaveData("dbo.sp4DFixedBetReset_Update", new { }, "DefaultConnection");
				}

				await Task.Delay(1000, stoppingToken);
			}
		}

		//Calculation for Number Variations
		private List<int> CalculateVariations(string number, string roll)
		{
			List<int> variations = new List<int>();
			switch (roll)
			{
				case "":
				case null:
					variations.Add(int.TryParse(number, out int parsedNumber) ? parsedNumber : 0);

					break;

				case "F":
					string numStrF = number;
					int lastDigitF = int.Parse(number.Substring(3, 1));
					numStrF = numStrF.Substring(0, 3);
					PermuteWithNoDuplicates(variations, numStrF.ToCharArray(), 0, 2);
					variations = variations.Select(v => int.Parse(v.ToString() + lastDigitF)).ToList();
					break;

				case "B":
					string numStrB = number;
					int firstDigitB = int.Parse(numStrB.Substring(0, 1));
					numStrB = numStrB.Substring(1, 3);
					PermuteWithNoDuplicates(variations, numStrB.ToCharArray(), 0, 2);
					variations = variations.Select(v => int.Parse(firstDigitB.ToString() + v.ToString())).ToList();
					break;

				case "R":
				case "I":
					PermuteWithNoDuplicates(variations, number.ToCharArray(), 0, 3);
					break;
			}
			return variations;
		}

		private void PermuteWithNoDuplicates(List<int> variations, char[] arr, int left, int right)
		{
			if (left == right)
			{
				int permutation = int.Parse(new string(arr));
				if (!variations.Contains(permutation))
				{
					variations.Add(permutation);
				}
			}
			else
			{
				for (int i = left; i <= right; i++)
				{
					Swap(ref arr[left], ref arr[i]);
					PermuteWithNoDuplicates(variations, arr, left + 1, right);
					Swap(ref arr[left], ref arr[i]); // backtrack
				}
			}
		}

		private void Swap(ref char a, ref char b)
		{
			char temp = a;
			a = b;
			b = temp;
		}

		//Calculation for Number Draw Dates
		private List<DateTime> GetDrawDatesForEntry(int input)
		{
			List<DateTime> dates = new List<DateTime>();
			DayOfWeek currentDay = DateTime.Now.DayOfWeek;
			TimeSpan currentTime = DateTime.Now.TimeOfDay;

			switch (input)
			{
				case 1:
					if (currentDay == DayOfWeek.Monday || currentDay == DayOfWeek.Tuesday)
					{
						dates.Add(GetNextWeekday(DayOfWeek.Wednesday));
						dates.Add(GetNextWeekday(DayOfWeek.Saturday));
						dates.Add(GetNextWeekday(DayOfWeek.Sunday));
					}
					else if (currentDay == DayOfWeek.Wednesday && currentTime < new TimeSpan(16, 30, 0))
					{
						dates.Add(GetNextWeekday(DayOfWeek.Wednesday));
						dates.Add(GetNextWeekday(DayOfWeek.Saturday));
						dates.Add(GetNextWeekday(DayOfWeek.Sunday));
					}
					else if (currentDay == DayOfWeek.Wednesday && currentTime >= new TimeSpan(16, 30, 0))
					{
						dates.Add(GetNextWeekday(DayOfWeek.Wednesday, 7));
						dates.Add(GetNextWeekday(DayOfWeek.Saturday));
						dates.Add(GetNextWeekday(DayOfWeek.Sunday));
					}
					else if (currentDay == DayOfWeek.Thursday || currentDay == DayOfWeek.Friday)
					{
						dates.Add(GetNextWeekday(DayOfWeek.Wednesday));
						dates.Add(GetNextWeekday(DayOfWeek.Saturday));
						dates.Add(GetNextWeekday(DayOfWeek.Sunday));
					}
					else if (currentDay == DayOfWeek.Saturday && currentTime < new TimeSpan(16, 30, 0))
					{
						dates.Add(GetNextWeekday(DayOfWeek.Saturday));
						dates.Add(GetNextWeekday(DayOfWeek.Wednesday));
						dates.Add(GetNextWeekday(DayOfWeek.Sunday));
					}
					else if (currentDay == DayOfWeek.Sunday && currentTime < new TimeSpan(16, 30, 0))
					{
						dates.Add(GetNextWeekday(DayOfWeek.Sunday));
					}
					else if (currentDay == DayOfWeek.Sunday && currentTime >= new TimeSpan(16, 30, 0))
					{
						dates.Add(GetNextWeekday(DayOfWeek.Wednesday));
						dates.Add(GetNextWeekday(DayOfWeek.Saturday));
						dates.Add(GetNextWeekday(DayOfWeek.Sunday, 7));
					}
					break;

				case 2:
					dates.Add(GetNextWeekday(DayOfWeek.Saturday));
					dates.Add(GetNextWeekday(DayOfWeek.Sunday));
					break;

				case 3:
					dates.Add(GetNextWeekday(DayOfWeek.Wednesday));
					break;

				case 6:
					dates.Add(GetNextWeekday(DayOfWeek.Saturday));
					break;

				case 7:
					dates.Add(GetNextWeekday(DayOfWeek.Sunday));
					break;

				default:
					break;
			}
			return dates;
		}

		private static DateTime GetDayInCurrentWeek(DateTime currentDate, DayOfWeek targetDay)
		{
			int daysUntilTargetDay = ((int)targetDay - (int)currentDate.DayOfWeek + 7) % 7;
			return currentDate.AddDays(daysUntilTargetDay);
		}

		private static DateTime GetNextWeekDay(DateTime currentDate, DayOfWeek targetDay)
		{
			int daysUntilTargetDay = ((int)targetDay - (int)currentDate.DayOfWeek + 7) % 7;
			return currentDate.AddDays(daysUntilTargetDay + 7);
		}

		private static DateTime GetNextWeekday(DayOfWeek day, int offset = 0)
		{
			int daysUntil = ((int)day - (int)DateTime.Now.DayOfWeek + 7) % 7 + offset;
			return DateTime.Now.Date.AddDays(daysUntil);
		}
	}
}