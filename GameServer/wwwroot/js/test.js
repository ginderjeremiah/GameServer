function getRandomNumArray(size) {
    let nums = [];
    for (let i = 0; i < size; i++) {
        nums.push(Math.random());
    }
    return nums;
}

function calcBezierOutput(x1, y1, x2, y2, t) {
    let co1 = 3*(1-t)**2 * t;
    let co2 = 3*(1-t)*t**2;
    let co3 = t**3;

    return {
        x: co1*x1 + co2*x2 + co3,
        y: co1*y1 + co2*y2 + co3
    }
}

function bezier1(x1, x2, t) {
    let co1 = 3*(1-t)**2 * t;
    let co2 = 3*(1-t)*t**2;
    let co3 = t**3;

    return co1*x1 + co2*x2 + co3;

}

function bezier1Loop(x1, x2) {
    for(let i = 1; i < 10; i++) {
        console.log(bezier1(x1, x2, 0.1*i));
    }
}

function bezierLoop(x1, y1, x2, y2) {
    for(let i = 1; i < 10; i++) {
        console.log(calcBezierOutput(x1, y1, x2, y2, 0.1*i));
    }
}


//for 0<=t<=1

// x = (1-t)x0 + tx1 => x = x0 + t*(x1 - x0) => t = (x - x0)/(x1 - x0)

//cur1,1 => t = (xa - x0)/(x1 - x0)
//cur1,2 => t = (xb - x1)/(x2 - x1)
//cur1,3 => t = (xc - x2)/(x3 - x2)

//cur2,1 => t = (xd - xa)/(xb - xa)
//cur2,2 => t = (xe - xb)/(xc - xb)



// xc = ((xa - x0) * (x3 - x2))/(x1 - x0) + x2



//cur3,1 => t = (x - xd)/(xe - xd)


//xe = xd + (x - xd)/t

//xe = t*(xc - xb) + xb


// (x - t*xe)/(1 - t) = t*(xb - xa) + xa

// (x - t^2(xc - xb) - txb) = txb - txa + xa - t^2(xb - xa) - txa

// t^2(xc - 2xb + xa) + t(2xb - 2xa) + xa - x = 0






//xd = (x - t*xe) /(1 - t)

//xd = t*(xb - xa) + xa


// xd + (x - xd)/t = t*(xc - xb) + xb

// txd + (x - xd) = t^2xc - t^2xb + txb

// t^2xb - t^2xa + txa + x - txb + txa - xa = t^2xc - t^2xb + txb

// t^2xb - t^2xa - t^2xc + t^2xb + 2txa - 2txb + x - xa = 0

//t^2(xb - xa - xc + xb) + 2t(xa - xb) + x - xa = 0

//t^2(2xb - xa - xc) + t(2xa - 2xb) + x - xa = 0


let inputs = document.getElementsByTagName('input');

for(let i = 0; i < inputs.length; i++) {
    let element = inputs[i];
    if(!element.id.startsWith('ratio')) {
        element.addEventListener('change', (event) => {
            let num = event.target.id.slice(-1);
            updateVal(num);
        })
    }
}

function updateVal(num) {
    let per = document.getElementById('permin' + num);
    let cost = document.getElementById('cost' + num);
    let rot = document.getElementById('ratio' + num);

    rot.value = parseFloat(per.value) / parseFloat(cost.value);
}

function getLinIncProbOdds(base /*base chance to hit*/, increase /*linear increase in chance to hit for each miss*/, rolls /*total number of rolls to calculate*/) {
    let bound01 = (num) => Math.min(1, Math.max(0, num)); //bound a number between 0 and 1
    let missBase = 1 - base;
    let total = rolls >= 0 ? bound01(missBase) : 0;
    for(let i = 1; i < rolls; i++) {
        total *= bound01(missBase - increase*i);
    }
    let avgRollsToHit;
    if(base > 0) {
        avgRollsToHit = (base*(-2) + increase + Math.sqrt(4*base*base - 4*base*increase + increase*(increase+8)))/(2*increase);
    } else {
        avgRollsToHit = (-base/increase) + (1 + Math.sqrt(1 + 8/increase))/(2);
    }
    return {
        "missOdds": total, //odds to not have hit in given amount of rolls
        "hitOdds": 1 - total, //odds to have hit in given amount of rolls
        "oneInXOdds":  1 / total, //average number of attempts for amount of rolls to miss
        "avgRollsToHit": isNaN(avgRollsToHit) ? "Cannot be hit on average" : avgRollsToHit //average rolls required to hit
    };
}