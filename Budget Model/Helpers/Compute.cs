using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Budget_Model.Helpers
{
    static class Compute
    {
        public class xirr
        {
            public static double tol = 0.001;
            public delegate double fx(double x);

            public static fx composeFunctions(fx f1, fx f2)
            {
                return (double x) => f1(x) + f2(x);
            }

            public static fx f_xirr(double p, double dt, double dt0)
            {
                return (double x) => p * Math.Pow((1.0 + x), ((dt0 - dt) / 365.0));
            }

            public static fx df_xirr(double p, double dt, double dt0)
            {
                return (double x) => (1.0 / 365.0) * (dt0 - dt) * p * Math.Pow((x + 1.0), (((dt0 - dt) / 365.0) - 1.0));
            }

            public static fx total_f_xirr(double[] payments, double[] days)
            {
                fx resf = (double x) => 0.0;

                for (int i = 0; i < payments.Length; i++)
                {
                    resf = composeFunctions(resf, f_xirr(payments[i], days[i], days[0]));
                }

                return resf;
            }

            public static fx total_df_xirr(double[] payments, double[] days)
            {
                fx resf = (double x) => 0.0;

                for (int i = 0; i < payments.Length; i++)
                {
                    resf = composeFunctions(resf, df_xirr(payments[i], days[i], days[0]));
                }

                return resf;
            }

            public static double Newtons_method(double guess, fx f, fx df)
            {
                double x0 = guess;
                double x1 = 0.0;
                double err = 1e+100;

                while (err > tol)
                {
                    x1 = x0 - f(x0) / df(x0);
                    err = Math.Abs(x1 - x0);
                    x0 = x1;
                }

                return x0;
            }
            public static double compute_yield_to_maturity(DateTime maturity, DateTime settlement, double price_paid, double coupon_rate, double par)
            {
                return compute_yield_to_maturity(maturity, settlement, price_paid, coupon_rate, par, 0.1);
            }
            public static double compute_yield_to_maturity(DateTime maturity, DateTime settlement, double price_paid, double coupon_rate, double par, double guess)
            {
                int nequalsemianns = (int)Math.Ceiling((maturity - settlement).TotalDays / (365.0 / 2.0));
                List<double> payments = new List<double>();
                List<double> datesAsDoubles = new List<double>();
                payments.Add(price_paid);
                datesAsDoubles.Add((settlement - DateTime.MinValue).TotalDays);
                for (int i = 1; i < nequalsemianns; i++)
                {
                    var totalDays = (maturity.AddMonths(-6 * i) - DateTime.MinValue).TotalDays;
                    datesAsDoubles.Add(totalDays);
                    payments.Add(coupon_rate / 2 * par);
                }
                payments.Add(par + (coupon_rate / 2 * par));
                datesAsDoubles.Add((maturity - DateTime.MinValue).TotalDays);

                double yield = Newtons_method(guess,
                        total_f_xirr(payments.ToArray(), datesAsDoubles.ToArray()),
                        total_df_xirr(payments.ToArray(), datesAsDoubles.ToArray()));
                return yield;
            }
        }
    }
}
