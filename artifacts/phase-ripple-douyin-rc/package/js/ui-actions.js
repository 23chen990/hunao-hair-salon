const { computeLayout } = require('./layout.js');
                                                        

                                                         

                                     
                           
                
               
              
                
                 
   

function resultActions(
  width        ,
  height        ,
  outcome                ,
)                          {
  if (outcome === 'playing') return [];
  const controls = computeLayout(width, height).controls;
  const top = controls.top + controls.height * 0.58;
  const buttonHeight = Math.max(36, width * 0.1);
  if (outcome === 'success') {
    return [{
      action: 'next',
      label: '继续',
      left: controls.left + controls.width * 0.18,
      top,
      width: controls.width * 0.64,
      height: buttonHeight,
    }];
  }
  const gap = 8;
  const buttonWidth = (controls.width - gap) / 2;
  return [
    { action: 'retry', label: '再试一次', left: controls.left, top, width: buttonWidth, height: buttonHeight },
    { action: 'hint', label: '看落点提示', left: controls.left + buttonWidth + gap, top, width: buttonWidth, height: buttonHeight },
  ];
}

function actionAtPoint(actions                         , point       )                          {
  const match = actions.find((entry) =>
    point.x >= entry.left && point.x <= entry.left + entry.width &&
    point.y >= entry.top && point.y <= entry.top + entry.height,
  );
  return match?.action ?? null;
}

module.exports.resultActions = resultActions;
module.exports.actionAtPoint = actionAtPoint;
