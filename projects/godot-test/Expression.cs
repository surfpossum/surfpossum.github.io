using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Expression {
    public List<Term> terms;
    string[] superscripts = new string[10]{"\u2070", "", "\u00B2", "\u00B3", "\u2074", "\u2075", "\u2076", "\u2077", "\u2078", "\u2079"};
    //\u00B9 is superscript 1

	public Expression () {
		terms = new List<Term>();
	}
    
	public Expression (Term t) {
		terms = new List<Term>();
        terms.Add(t);
	}

	public Expression (Expression e) {
		terms = new List<Term>();
		SetExpression(e);
	}
    
    public void SetExpression (int coeff, int xPow, int yPow, int zPow) {
        terms.Clear();
        Term t = new Term(coeff, xPow, yPow, zPow);
        //Debug.Log(t + " term");
        terms.Add(t);
        //Debug.Log(this);
        //Debug.Log("marP");
    }
    
    public void SetExpression (Term t) {
        terms.Clear();
        terms.Add(new Term(t));
        //Debug.Log("merP");
    }
    
    public void SetExpression (Expression exp) {
        terms.Clear();
        foreach (Term t in exp.terms) {
            //Debug.Log(t);
            terms.Add(new Term(t));
        }
    }
	
	public bool IsEqual(Expression exp) {
		Expression result = new Expression();
        result.SetExpression(this.SubtractExpression(exp));
		if (result.IsEmpty())
			return true;
		else
			return false;
	}
    
    public Expression AddExpression (Expression newExp) {
        Expression result = new Expression();
        result.SetExpression(this);
        for (int i = 0; i < newExp.terms.Count; i++)
            result.AddTerm(new Term(newExp.terms[i].coeff, newExp.terms[i].xPow, newExp.terms[i].yPow, newExp.terms[i].zPow));
        result.OrderExp();
        return result;
    }
    
    public Expression SubtractExpression (Expression newExp) {
        Expression result = new Expression();
        result.SetExpression(this);
        for (int i = 0; i < newExp.terms.Count; i++)
            result.AddTerm(new Term(-newExp.terms[i].coeff, newExp.terms[i].xPow, newExp.terms[i].yPow, newExp.terms[i].zPow));
        //Debug.Log(result);
        result.OrderExp();
        return result;
    }
    
    public Expression MakeNegative () {
        Expression result = new Expression();
        result = result.SubtractExpression(this);
        //Debug.Log(result);
        result.OrderExp();
        return result;
    }
    
    public Expression MultiplyExpression (Expression newExp) {
        Expression result = new Expression();
        for (int i = 0; i < newExp.terms.Count; i++) {
            //Debug.Log("multexp " + newExp.terms[i]);
            result = result.AddExpression(MultiplyByTerm(newExp.terms[i]));
        }
        result.OrderExp();
        return result;
    }
    
    //run through divisions on a temporary expression until we know it all succeeds
    public Expression DivideExpression (Expression divisor) {
        Expression result = new Expression();
        Expression tempDividend = new Expression();
        Expression quotient = new Expression();
        bool success = true;
        tempDividend.SetExpression(this);
        //prevent infinite loops
        int loopbreaker = 0;
        while (success && loopbreaker < 100) {
            loopbreaker++;
            //if tempDividend is empty, division was a success
            if (tempDividend.IsEmpty()) {
                //Debug.Log("success");
                success = true;
                result.terms = quotient.terms;
                result.OrderExp();
                break;
            }
            //if the leading term of the dividend is divisible by the leading term of the divisor, proceed
            if (tempDividend.terms[0].IsDivisible(divisor.terms[0])) {
                //store the result of dividing the leading term in the quotient
                //Debug.Log("dividend " + tempDividend);
                //Debug.Log("divisor term " + divisor.terms[0]);
                Term qt = tempDividend.terms[0].DivideByTerm(divisor.terms[0]);
                //Debug.Log("qt " + qt);
                quotient.AddTerm(qt);
                //Debug.Log("quotient " + quotient);
                //subtract the result of multiplying the divisor by the term added to the quotient
                tempDividend = tempDividend.SubtractExpression(divisor.MultiplyByTerm(qt));
                //Debug.Log("dividend subtracted " + tempDividend);
                tempDividend.terms.RemoveAll(zeroTerm);
                //Debug.Log("dividend zeroed " + tempDividend);
            }
            else {
                //Debug.Log("failed?");
                result.terms.Clear();
                success = false;
                break;
            }
        }
        
        return result;
    }
    
    private static bool zeroTerm(Term t) {
        return t.coeff == 0;
    }
	
	//adds term
	public void AddTerm (Term newTerm) {
        //Debug.Log("new term " + newTerm);
        if (terms.Count == 0)
            terms.Add(newTerm);
        else {
            for (int j = 0; j < terms.Count; j++)
            {
                if(terms[j].IsLikeTerm(newTerm)) {
                    terms[j].coeff += newTerm.coeff;
                    //Debug.Log("like term found at " + j);
                    break;
                }
                else if (j == terms.Count - 1) {
                    terms.Add(newTerm);
                    //Debug.Log("like term not found; " + terms.Count);
                    break;
                }
            }
        }
        OrderExp();
	}
    
    public bool IsEmpty() {
        return (terms.Count == 0);
    }
    
    public bool IsConstant() {
		return (terms.Count == 0 ||(terms.Count == 1 && terms[0].xPow == 0 && terms[0].yPow == 0 && terms[0].zPow == 0));
    }
	
	//multiplies expression by term, returns resulting expression
	public Expression MultiplyByTerm (Term newTerm) {
        //Debug.Log("new term " + newTerm);
        //Debug.Log("multterm " + newTerm);
        Expression result = new Expression();
        for (int j = 0; j < terms.Count; j++)
        {
            Term m = terms[j].MultiplyByTerm(newTerm);
            //Debug.Log("m " + m);
            result.AddTerm(m);           
        }
        //Debug.Log(result);
        return result;
	}
    
    public bool Operate(Expression operand, string operation) {
        bool success = false;
        Expression result = new Expression();
        switch (operation) {
        case "+":
            result = AddExpression(operand);
            break;
        case "-":
            result = SubtractExpression(operand);
            break;
        case "*":
            result = MultiplyExpression(operand);
            break;
        case "/":
            result = DivideExpression(operand);
            break;
        default:
            break;
        }
        if (!result.IsEmpty()) {
            SetExpression(result);
            terms.RemoveAll(zeroTerm);
            success = true;
        }
        //Debug.Log("operation " + success);
        return success;
    }
    
    //compares two terms based on degree in x, then degree in y, then degree in z, then coefficient
    //returns 1 if t1 is greater, -1 if t2 is greater, 0 if they're equal
    private static int CompareTerms(Term t1, Term t2) {
        if (t1.xPow > t2.xPow)
            return -1;
        else if (t1.xPow < t2.xPow)
            return 1;
        else {
            if (t1.yPow > t2.yPow)
                return -1;
            else if (t1.yPow < t2.yPow)
                return 1;
            else {
                if (t1.zPow > t2.zPow)
                    return -1;
                else if (t1.zPow < t2.zPow)
                    return 1;
                else {
                    if (t1.coeff > t2.coeff)
                        return -1;
                    else if (t1.coeff < t2.coeff)
                        return 1;
                    else {
                        return 0;
                    }
                }
            }
        }
    }
    
    public void OrderExp() {
        terms.Sort(CompareTerms);
        terms.RemoveAll(zeroTerm);
    }
    
    public override string ToString() {
        string result = "";
        for (int i = 0; i < terms.Count; i++) {
            if (i > 0)
                result = result + "\u00A0+\u00A0";
            if (terms[i].xPow == 0 && terms[i].yPow == 0 && terms[i].zPow == 0)
                result = result + terms[i].coeff;
            else {
                string cStr = (terms[i].coeff != 1) ? ((terms[i].coeff == -1) ? "-" : "" + terms[i].coeff) : "";
                string xStr = (terms[i].xPow != 0) ? "x" + superscripts[terms[i].xPow] : "";
                string yStr = (terms[i].yPow != 0) ? "y" + superscripts[terms[i].yPow] : "";
                string zStr = (terms[i].zPow != 0) ? "z" + superscripts[terms[i].zPow] : "";
                result = result + cStr + xStr + yStr + zStr;
            }
        }
		if (result.Equals(""))
			result = "0";
        return result;
    }
    
    public Expression GenerateRandom(int terms, int coeffMax, int xPowMax, int yPowMax, int zPowMax) {
        Expression result = new Expression();
        for (int i = 0; i < terms; i++) {
            Term temp = new Term(UnityEngine.Random.Range(1, coeffMax), UnityEngine.Random.Range(0, xPowMax), UnityEngine.Random.Range(0, yPowMax), UnityEngine.Random.Range(0, zPowMax));
            result.AddTerm(temp);
        }
        return result;
    }
    
    public int CountTerms() {
        return terms.Count;
    }
    
}
