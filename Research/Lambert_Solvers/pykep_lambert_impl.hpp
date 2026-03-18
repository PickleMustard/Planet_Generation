/**
 * PyKEP Lambert Problem Solver Implementation
 * =============================================
 * 
 * This is the complete source code from PyKEP's lambert_problem implementation.
 * Source: https://github.com/esa/pykep
 * Files: 
 *   - src/lambert_problem.cpp (implementation)
 *   - include/keplerian_toolbox/lambert_problem.hpp (header)
 * 
 * This is the Dario Izzo algorithm which is the same algorithm used in poliastro.
 * The C++ implementation includes multi-revolution support and uses Householder
 * iterations for finding the solution.
 * 
 * Author: Dario Izzo (dario.izzo AT googlemail.com)
 * Copyright: ESA - European Space Agency
 */

#ifndef KEP_TOOLBOX_LAMBERT_PROBLEM_H
#define KEP_TOOLBOX_LAMBERT_PROBLEM_H

#include <cmath>
#include <vector>

#include <keplerian_toolbox/astro_constants.hpp>
#include <keplerian_toolbox/detail/visibility.hpp>
#include <keplerian_toolbox/serialization.hpp>

namespace kep_toolbox
{

/// Lambert Problem
/**
 * This class represent a Lambert's problem. When instantiated it assumes a prograde orbit 
 * (unless otherwise stated) and evaluates all the solutions up to a maximum number of 
 * multiple revolutions.
 * 
 * NOTE: The class has been tested extensively via monte carlo runs checked with numerical 
 * propagation. Compared to the previous Lambert Solver in the keplerian_toolbox it is 
 * 1.7 times faster (on average as defined by lambert_test.cpp). With respect to Gooding 
 * algorithm it is 1.3 - 1.5 times faster (zero revs - multi revs).
 * 
 * @author Dario Izzo (dario.izzo _AT_ googlemail.com)
 */

class KEP_TOOLBOX_DLL_PUBLIC lambert_problem;

// Streaming operator
KEP_TOOLBOX_DLL_PUBLIC std::ostream &operator<<(std::ostream &, const lambert_problem &);

class KEP_TOOLBOX_DLL_PUBLIC lambert_problem
{
    static const array3D default_r1;
    static const array3D default_r2;

public:
    friend KEP_TOOLBOX_DLL_PUBLIC std::ostream &operator<<(std::ostream &, const lambert_problem &);
    
    /// Constructor
    /**
     * Constructs and solves a Lambert problem.
     *
     * @param[in] r1 first cartesian position
     * @param[in] r2 second cartesian position
     * @param[in] tof time of flight
     * @param[in] mu gravity parameter
     * @param[in] cw when 1 a retrograde orbit is assumed
     * @param[in] multi_revs maximum number of multirevolutions to compute
     */
    lambert_problem(const array3D &r1 = default_r1, const array3D &r2 = default_r2,
                    const double &tof = boost::math::constants::pi<double>() / 2, const double &mu = 1.,
                    const int &cw = 0, const int &multi_revs = 5);
                    
    const std::vector<array3D> &get_v1() const;  ///< Get velocity at r1
    const std::vector<array3D> &get_v2() const;  ///< Get velocity at r2
    const array3D &get_r1() const;               ///< Get r1
    const array3D &get_r2() const;               ///< Get r2
    const double &get_tof() const;               ///< Get time of flight
    const double &get_mu() const;                ///< Get gravitational parameter
    const std::vector<double> &get_x() const;    ///< Get x variables
    const std::vector<int> &get_iters() const;   ///< Get iteration counts
    int get_Nmax() const;                        ///< Get maximum number of revolutions

private:
    // ========================================================================
    // Private member functions
    // ========================================================================
    
    /// Householder iteration method for finding x
    /**
     * Find the root of the TOF equation using Householder's method
     * 
     * @param[in] T Target non-dimensional time of flight
     * @param[in,out] x0 Initial guess, updated to solution
     * @param[in] N Number of revolutions
     * @param[in] eps Convergence tolerance
     * @param[in] iter_max Maximum iterations
     * @return Number of iterations performed
     */
    int householder(const double T, double &x0, const int N, const double eps, const int itermax);
    
    /// Compute derivatives of TOF with respect to x
    /**
     * Computes dT/dx, d2T/dx2, d3T/dx3 for the TOF equation
     * 
     * @param[out] DT First derivative dT/dx
     * @param[out] DDT Second derivative d2T/dx2
     * @param[out] DDDT Third derivative d3T/dx3
     * @param[in] x The x parameter
     * @param[in] T The non-dimensional time of flight
     */
    void dTdx(double &DT, double &DDT, double &DDDT, const double x0, const double tof);
    
    /// Convert x to time of flight (main function)
    /**
     * This is the main TOF equation. It uses different series expansions
     * depending on the value of x to maintain numerical accuracy.
     * 
     * @param[out] tof Computed non-dimensional time of flight
     * @param[in] x The x parameter
     * @param[in] N Number of revolutions
     */
    void x2tof(double &tof, const double x0, const int N);
    
    /// Convert x to time of flight (Lagrange form)
    /**
     * Uses the Lagrange form of the TOF equation
     * 
     * @param[out] tof Computed non-dimensional time of flight
     * @param[in] x The x parameter
     * @param[in] N Number of revolutions
     */
    void x2tof2(double &tof, const double x0, const int N);
    
    /// Hypergeometric function computation
    /**
     * Computes the hypergeometric function F(z) used in the Battin series
     * 
     * @param[in] z Input parameter
     * @param[in] tol Convergence tolerance
     * @return The hypergeometric function value
     */
    double hypergeometricF(double z, double tol);
    
    // Boost serialization
    friend class boost::serialization::access;
    template <class Archive>
    void serialize(Archive &ar, const unsigned int)
    {
        ar &const_cast<array3D &>(m_r1);
        ar &const_cast<array3D &>(m_r2);
        ar &const_cast<double &>(m_tof);
        ar &const_cast<double &>(m_mu);
        ar &m_v1;
        ar &m_v2;
        ar &m_iters;
        ar &m_x;
        ar &m_s;
        ar &m_c;
        ar &m_lambda;
        ar &m_iters;
        ar &m_Nmax;
        ar &m_has_converged;
        ar &m_multi_revs;
    }

    // ========================================================================
    // Private member variables
    // ========================================================================
    
    const array3D m_r1, m_r2;        ///< Initial and final position vectors
    const double m_tof;               ///< Time of flight
    const double m_mu;                ///< Gravitational parameter
    std::vector<array3D> m_v1;        ///< Velocity solutions at r1
    std::vector<array3D> m_v2;        ///< Velocity solutions at r2
    std::vector<int> m_iters;        ///< Iteration counts for each solution
    std::vector<double> m_x;          ///< x parameter for each solution
    double m_s;                        ///< Semi-perimeter
    double m_c;                        ///< Chord length
    double m_lambda;                   ///< Lambda parameter (geometry)
    int m_Nmax;                        ///< Maximum number of revolutions
    bool m_has_converged;              ///< Convergence flag
    int m_multi_revs;                  ///< Requested maximum revolutions
};

} // namespace kep_toolbox

#endif // KEP_TOOLBOX_LAMBERT_PROBLEM_H


/*****************************************************************************
 *   Implementation of lambert_problem.cpp
 *****************************************************************************/

#include <boost/math/special_functions/acosh.hpp>
#include <boost/math/special_functions/asinh.hpp>

#include <keplerian_toolbox/core_functions/array3D_operations.hpp>
#include <keplerian_toolbox/exceptions.hpp>
#include <keplerian_toolbox/lambert_problem.hpp>

namespace kep_toolbox
{

const array3D lambert_problem::default_r1 = {{1.0, 0.0, 0.0}};
const array3D lambert_problem::default_r2 = {{0.0, 1.0, 0.0}};

/// Constructor - Full Implementation
lambert_problem::lambert_problem(const array3D &r1, const array3D &r2, const double &tof, const double &mu,
                                 const int &cw, const int &multi_revs)
    : m_r1(r1), m_r2(r2), m_tof(tof), m_mu(mu), m_has_converged(true), m_multi_revs(multi_revs)
{
    // ========================================================================
    // 0 - Sanity checks
    // ========================================================================
    if (tof <= 0) {
        throw_value_error("Time of flight is negative!");
    }
    if (mu <= 0) {
        throw_value_error("Gravity parameter is zero or negative!");
    }
    
    // ========================================================================
    // 1 - Getting lambda and computing geometry
    // ========================================================================
    
    // Chord length
    m_c = sqrt((r2[0] - r1[0]) * (r2[0] - r1[0]) + (r2[1] - r1[1]) * (r2[1] - r1[1])
               + (r2[2] - r1[2]) * (r2[2] - r1[2]));
    
    // Norms of position vectors
    double R1 = norm(m_r1);
    double R2 = norm(m_r2);
    
    // Semi-perimeter
    m_s = (m_c + R1 + R2) / 2.0;
    
    // Unit vectors
    array3D ir1, ir2, ih, it1, it2;
    vers(ir1, r1);
    vers(ir2, r2);
    
    // Angular momentum direction
    cross(ih, ir1, ir2);
    vers(ih, ih);
    
    if (ih[2] == 0) {
        throw_value_error("The angular momentum vector has no z component, impossible to define "
                         "automatically clock or counterclockwise");
    }
    
    // ========================================================================
    // Compute lambda (geometry parameter)
    // lambda = sqrt(1 - c/s)  (with sign handling)
    // ========================================================================
    double lambda2 = 1.0 - m_c / m_s;
    m_lambda = sqrt(lambda2);

    // Handle transfer angle > 180 degrees (as seen from above z-axis)
    if (ih[2] < 0.0) 
    {
        m_lambda = -m_lambda;
        cross(it1, ir1, ih);
        cross(it2, ir2, ih);
    } else {
        cross(it1, ih, ir1);
        cross(it2, ih, ir2);
    }
    vers(it1, it1);
    vers(it2, it2);

    // Handle retrograde motion (cw = 1)
    if (cw) { // Retrograde motion
        m_lambda = -m_lambda;
        it1[0] = -it1[0];
        it1[1] = -it1[1];
        it1[2] = -it1[2];
        it2[0] = -it2[0];
        it2[1] = -it2[1];
        it2[2] = -it2[2];
    }
    
    // ========================================================================
    // Non-dimensional time of flight
    // ========================================================================
    double lambda3 = m_lambda * lambda2;
    double T = sqrt(2.0 * m_mu / m_s / m_s / m_s) * m_tof;

    // ========================================================================
    // 2 - Find all x solutions
    // ========================================================================
    
    // 2.1 - Detect maximum number of revolutions
    m_Nmax = static_cast<int>(T / M_PI);
    double T00 = acos(m_lambda) + m_lambda * sqrt(1.0 - lambda2);
    double T0 = (T00 + m_Nmax * M_PI);
    double T1 = 2.0 / 3.0 * (1.0 - lambda3), DT = 0.0, DDT = 0.0, DDDT = 0.0;
    
    if (m_Nmax > 0) {
        if (T < T0) { // Use Halley iterations to find xM and TM
            int it = 0;
            double err = 1.0;
            double T_min = T0;
            double x_old = 0.0, x_new = 0.0;
            while (1) {
                dTdx(DT, DDT, DDDT, x_old, T_min);
                if (DT != 0.0) {
                    x_new = x_old - DT * DDT / (DDT * DDT - DT * DDDT / 2.0);
                }
                err = fabs(x_old - x_new);
                if ((err < 1e-13) || (it > 12)) {
                    break;
                }
                x2tof(T_min, x_new, m_Nmax);
                x_old = x_new;
                it++;
            }
            if (T_min > T) {
                m_Nmax -= 1;
            }
        }
    }
    
    // Crop to requested multi_revs
    m_Nmax = std::min(m_multi_revs, m_Nmax);

    // 2.2 - Allocate output vectors
    m_v1.resize(static_cast<size_t>(m_Nmax) * 2 + 1);
    m_v2.resize(static_cast<size_t>(m_Nmax) * 2 + 1);
    m_iters.resize(static_cast<size_t>(m_Nmax) * 2 + 1);
    m_x.resize(static_cast<size_t>(m_Nmax) * 2 + 1);

    // ========================================================================
    // 3 - Find all solutions in x, y
    // ========================================================================
    
    // 3.1 - Zero revolution solution
    // 3.1.1 - Initial guess
    if (T >= T00) {
        m_x[0] = -(T - T00) / (T - T00 + 4);
    } else if (T <= T1) {
        m_x[0] = T1 * (T1 - T) / (2.0 / 5.0 * (1 - lambda2 * lambda3) * T) + 1;
    } else {
        m_x[0] = pow((T / T00), 0.69314718055994529 / log(T1 / T00)) - 1.0;
    }
    
    // 3.1.2 - Householder iterations for 0 revs
    m_iters[0] = householder(T, m_x[0], 0, 1e-5, 15);
    
    // 3.2 - Multi-revolution solutions
    double tmp;
    for (decltype(m_Nmax) i = 1; i < m_Nmax + 1; ++i) {
        // 3.2.1 - Left branch (low path) Householder iterations
        tmp = pow((i * M_PI + M_PI) / (8.0 * T), 2.0 / 3.0);
        m_x[2 * i - 1] = (tmp - 1) / (tmp + 1);
        m_iters[2 * i - 1] = householder(T, m_x[2 * i - 1], i, 1e-8, 15);
        
        // 3.2.2 - Right branch (high path) Householder iterations
        tmp = pow((8.0 * T) / (i * M_PI), 2.0 / 3.0);
        m_x[2 * i] = (tmp - 1) / (tmp + 1);
        m_iters[2 * i] = householder(T, m_x[2 * i], i, 1e-8, 15);
    }

    // ========================================================================
    // 4 - Reconstruct terminal velocities from x
    // ========================================================================
    double gamma = sqrt(m_mu * m_s / 2.0);
    double rho = (R1 - R2) / m_c;
    double sigma = sqrt(1 - rho * rho);
    double vr1, vt1, vr2, vt2, y;
    
    for (size_t i = 0; i < m_x.size(); ++i) {
        // Compute y from x and lambda
        // y = sqrt(1 - lambda^2 + lambda^2 * x^2)
        y = sqrt(1.0 - lambda2 + lambda2 * m_x[i] * m_x[i]);
        
        // Radial velocity components
        vr1 = gamma * ((m_lambda * y - m_x[i]) - rho * (m_lambda * y + m_x[i])) / R1;
        vr2 = -gamma * ((m_lambda * y - m_x[i]) + rho * (m_lambda * y + m_x[i])) / R2;
        
        // Tangential velocity component
        double vt = gamma * sigma * (y + m_lambda * m_x[i]);
        vt1 = vt / R1;
        vt2 = vt / R2;
        
        // Construct velocity vectors: v = vr * ir + vt * it
        for (int j = 0; j < 3; ++j)
            m_v1[i][j] = vr1 * ir1[j] + vt1 * it1[j];
        for (int j = 0; j < 3; ++j)
            m_v2[i][j] = vr2 * ir2[j] + vt2 * it2[j];
    }
}

/// Householder iteration method
/**
 * Finds the root of the TOF equation using Householder's method.
 * This is a quartic method that converges faster than Newton or Halley.
 * 
 * The Householder iteration formula:
 * x_new = x - f * (f'^2 - f*f''/2) / (f' * (f'^2 - f*f'') + f'''*f^2/6)
 * 
 * @param[in] T Target non-dimensional time of flight
 * @param[in,out] x0 Initial guess, updated to solution
 * @param[in] N Number of revolutions
 * @param[in] eps Convergence tolerance
 * @param[in] iter_max Maximum iterations
 * @return Number of iterations performed
 */
int lambert_problem::householder(const double T, double &x0, const int N, const double eps, const int iter_max)
{
    int it = 0;
    double err = 1.0;
    double xnew = 0.0;
    double tof = 0.0, delta = 0.0, DT = 0.0, DDT = 0.0, DDDT = 0.0;
    
    while ((err > eps) && (it < iter_max)) {
        // Compute TOF for current x
        x2tof(tof, x0, N);
        
        // Compute derivatives
        dTdx(DT, DDT, DDDT, x0, tof);
        
        // Compute difference from target
        delta = tof - T;
        double DT2 = DT * DT;
        
        // Householder step (quartic convergence)
        xnew = x0 - delta * (DT2 - delta * DDT / 2.0) 
                     / (DT * (DT2 - delta * DDT) + DDDT * delta * delta / 6.0);
        
        err = fabs(x0 - xnew);
        x0 = xnew;
        it++;
    }
    return it;
}

/// Compute derivatives of TOF with respect to x
/**
 * Computes first, second, and third derivatives of the TOF equation
 * with respect to x. These are used in the Householder iteration.
 * 
 * Formulas:
 * dT/dx   = (3*T*x - 2 + 2*lambda^3*x/y) / (1 - x^2)
 * d2T/dx2 = (3*T + 5*x*dT/dx + 2*(1-lambda^2)*lambda^3/y^3) / (1 - x^2)
 * d3T/dx3 = (7*x*d2T/dx2 + 8*dT/dx - 6*(1-lambda^2)*lambda^5*x/y^5) / (1 - x^2)
 * 
 * @param[out] DT First derivative dT/dx
 * @param[out] DDT Second derivative d2T/dx2
 * @param[out] DDDT Third derivative d3T/dx3
 * @param[in] x The x parameter
 * @param[in] T The non-dimensional time of flight
 */
void lambert_problem::dTdx(double &DT, double &DDT, double &DDDT, const double x, const double T)
{
    double l2 = m_lambda * m_lambda;
    double l3 = l2 * m_lambda;
    double umx2 = 1.0 - x * x;
    double y = sqrt(1.0 - l2 * umx2);
    double y2 = y * y;
    double y3 = y2 * y;
    
    DT = 1.0 / umx2 * (3.0 * T * x - 2.0 + 2.0 * l3 * x / y);
    DDT = 1.0 / umx2 * (3.0 * T + 5.0 * x * DT + 2.0 * (1.0 - l2) * l3 / y3);
    DDDT = 1.0 / umx2 * (7.0 * x * DDT + 8.0 * DT - 6.0 * (1.0 - l2) * l2 * l3 * x / y3 / y2);
}

/// Convert x to TOF using Lagrange form
/**
 * Uses the standard Lagrange form of the TOF equation.
 * Works for all orbit types (elliptic, hyperbolic, parabolic).
 * 
 * For elliptic (a > 0, x^2 < 1):
 *   alfa = 2*acos(x), beta = 2*asin(sqrt(lambda^2/a))
 *   tof = a*sqrt(a)*((alfa-sin(alfa))-(beta-sin(beta))+2*pi*N)/2
 * 
 * For hyperbolic (a < 0, x^2 > 1):
 *   alfa = 2*acosh(x), beta = 2*asinh(sqrt(-lambda^2/a))
 *   tof = -a*sqrt(-a)*((beta-sinh(beta))-(alfa-sinh(alfa)))/2
 * 
 * @param[out] tof Computed non-dimensional time of flight
 * @param[in] x The x parameter
 * @param[in] N Number of revolutions
 */
void lambert_problem::x2tof2(double &tof, const double x, const int N)
{
    double a = 1.0 / (1.0 - x * x);
    
    if (a > 0) // ellipse
    {
        double alfa = 2.0 * acos(x);
        double beta = 2.0 * asin(sqrt(m_lambda * m_lambda / a));
        if (m_lambda < 0.0) beta = -beta;
        tof = ((a * sqrt(a) * ((alfa - sin(alfa)) - (beta - sin(beta)) + 2.0 * M_PI * N)) / 2.0);
    } 
    else // hyperbolic
    {
        double alfa = 2.0 * boost::math::acosh(x);
        double beta = 2.0 * boost::math::asinh(sqrt(-m_lambda * m_lambda / a));
        if (m_lambda < 0.0) beta = -beta;
        tof = (-a * sqrt(-a) * ((beta - sinh(beta)) - (alfa - sinh(alfa))) / 2.0);
    }
}

/// Convert x to TOF - main function
/**
 * This is the main function that converts the x parameter to time of flight.
 * It uses different series expansions depending on the value of x to maintain
 * numerical accuracy:
 * 
 * 1. Near x = 1 (parabolic): Uses Lagrange form (x2tof2)
 * 2. Near x = 1 (Battin series): For intermediate values with better convergence
 * 3. Lancaster form: For values far from x = 1
 * 
 * The Battin series uses the hypergeometric function for better numerical
 * performance near the parabolic case.
 * 
 * @param[out] tof Computed non-dimensional time of flight
 * @param[in] x The x parameter
 * @param[in] N Number of revolutions
 */
void lambert_problem::x2tof(double &tof, const double x, const int N)
{
    double battin = 0.01;
    double lagrange = 0.2;
    double dist = fabs(x - 1);
    
    // Use Lagrange form when close to parabolic
    if (dist < lagrange && dist > battin) { 
        x2tof2(tof, x, N);
        return;
    }
    
    double K = m_lambda * m_lambda;
    double E = x * x - 1.0;
    double rho = fabs(E);
    double z = sqrt(1 + K * E);
    
    // Use Battin series when very close to parabolic
    if (dist < battin) { 
        double eta = z - m_lambda * x;
        double S1 = 0.5 * (1.0 - m_lambda - x * eta);
        double Q = hypergeometricF(S1, 1e-11);
        Q = 4.0 / 3.0 * Q;
        tof = (eta * eta * eta * Q + 4.0 * m_lambda * eta) / 2.0 + N * M_PI / pow(rho, 1.5);
        return;
    }
    
    // Use Lancaster form for general case
    double y = sqrt(rho);
    double g = x * z - m_lambda * E;
    double d = 0.0;
    
    if (E < 0) { // Elliptic
        double l = acos(g);
        d = N * M_PI + l;
    } else { // Hyperbolic
        double f = y * (z - m_lambda * x);
        d = log(f + g);
    }
    
    tof = (x - m_lambda * z - d / y) / E;
}

/// Hypergeometric function F(z)
/**
 * Computes the hypergeometric function F(z) used in the Battin series.
 * This is a continued fraction expansion that converges quickly.
 * 
 * F(z) = 1 + (3*1/2.5)z + (3*1*5/2.5*3.5)(z^2)/2! + ...
 * 
 * @param[in] z Input parameter
 * @param[in] tol Convergence tolerance
 * @return The hypergeometric function value
 */
double lambert_problem::hypergeometricF(double z, double tol)
{
    double Sj = 1.0;
    double Cj = 1.0;
    double err = 1.0;
    double Cj1 = 0.0;
    double Sj1 = 0.0;
    int j = 0;
    
    while (err > tol) {
        Cj1 = Cj * (3.0 + j) * (1.0 + j) / (2.5 + j) * z / (j + 1);
        Sj1 = Sj + Cj1;
        err = fabs(Cj1);
        Sj = Sj1;
        Cj = Cj1;
        j = j + 1;
    }
    return Sj;
}

// ============================================================================
// Getters
// ============================================================================

const std::vector<array3D> &lambert_problem::get_v1() const
{
    return m_v1;
}

const std::vector<array3D> &lambert_problem::get_v2() const
{
    return m_v2;
}

const array3D &lambert_problem::get_r1() const
{
    return m_r1;
}

const array3D &lambert_problem::get_r2() const
{
    return m_r2;
}

const double &lambert_problem::get_tof() const
{
    return m_tof;
}

const std::vector<double> &lambert_problem::get_x() const
{
    return m_x;
}

const double &lambert_problem::get_mu() const
{
    return m_mu;
}

const std::vector<int> &lambert_problem::get_iters() const
{
    return m_iters;
}

int lambert_problem::get_Nmax() const
{
    return m_Nmax;
}

/// Streaming operator
std::ostream &operator<<(std::ostream &s, const lambert_problem &lp)
{
    s << std::setprecision(14) << "Lambert's problem:" << std::endl;
    s << "mu = " << lp.m_mu << std::endl;
    s << "r1 = " << lp.m_r1 << std::endl;
    s << "r2 = " << lp.m_r2 << std::endl;
    s << "Time of flight: " << lp.m_tof << std::endl << std::endl;
    s << "chord = " << lp.m_c << std::endl;
    s << "semiperimeter = " << lp.m_s << std::endl;
    s << "lambda = " << lp.m_lambda << std::endl;
    s << "non dimensional time of flight = " << lp.m_tof * sqrt(2 * lp.m_mu / lp.m_s / lp.m_s / lp.m_s) << std::endl
      << std::endl;
    s << "Maximum number of revolutions: " << lp.m_Nmax << std::endl;
    s << "Solutions: " << std::endl;
    s << "0 revs, Iters: " << lp.m_iters[0] << ", x: " << lp.m_x[0]
      << ", a: " << lp.m_s / 2.0 / (1 - lp.m_x[0] * lp.m_x[0]) << std::endl;
    s << "\tv1= " << lp.m_v1[0] << " v2= " << lp.m_v2[0] << std::endl;
    for (int i = 0; i < lp.m_Nmax; ++i) {
        s << i + 1 << " revs,  left. Iters: " << lp.m_iters[1 + 2 * i] << ", x: " << lp.m_x[1 + 2 * i]
          << ", a: " << lp.m_s / 2.0 / (1 - lp.m_x[1 + 2 * i] * lp.m_x[1 + 2 * i]) << std::endl;
        s << "\tv1= " << lp.m_v1[1 + 2 * i] << " v2= " << lp.m_v2[1 + 2 * i] << std::endl;
        s << i + 1 << " revs, right. Iters: " << lp.m_iters[2 + 2 * i] << ", a: " << lp.m_x[2 + 2 * i]
          << ", a: " << lp.m_s / 2.0 / (1 - lp.m_x[2 + 2 * i] * lp.m_x[2 + 2 * i]) << std::endl;
        s << "\tv1= " << lp.m_v1[2 + 2 * i] << " v2= " << lp.m_v2[2 + 2 * i] << std::endl;
    }
    return s;
}

} // namespace kep_toolbox
