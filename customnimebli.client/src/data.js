import textsJson from './texts.json';

export const ukText = textsJson;

export const services = [
  { id: 1, title: ukText.kitchens, description: ukText.kitchensDesc, icon: '??' },
  { id: 2, title: ukText.wardrobes, description: ukText.wardrobesDesc, icon: '??' },
  { id: 3, title: ukText.hallways, description: ukText.hallwaysDesc, icon: '??' }
];

export const advantages = [
  { id: 1, icon: '??', title: ukText.advExperience, desc: ukText.advExperienceDesc },
  { id: 2, icon: '?', title: ukText.advQuality, desc: ukText.advQualityDesc },
  { id: 3, icon: '???', title: ukText.advWarranty, desc: ukText.advWarrantyDesc },
  { id: 4, icon: '??', title: ukText.advIndividual, desc: ukText.advIndividualDesc }
];

export const steps = [
  { id: 1, title: ukText.step1, desc: ukText.step1Desc, icon: '??' },
  { id: 2, title: ukText.step2, desc: ukText.step2Desc, icon: '??' },
  { id: 3, title: ukText.step3, desc: ukText.step3Desc, icon: '??' },
  { id: 4, title: ukText.step4, desc: ukText.step4Desc, icon: '???' },
  { id: 5, title: ukText.step5, desc: ukText.step5Desc, icon: '?' }
];

export const faqItems = [
  { id: 1, question: ukText.faq1q, answer: ukText.faq1a },
  { id: 2, question: ukText.faq2q, answer: ukText.faq2a },
  { id: 3, question: ukText.faq3q, answer: ukText.faq3a },
  { id: 4, question: ukText.faq4q, answer: ukText.faq4a }
];

export const categories = [ukText.kitchens, ukText.wardrobes, ukText.hallways];
