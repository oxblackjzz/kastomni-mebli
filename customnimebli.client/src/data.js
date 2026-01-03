// Ukrainian texts for the furniture website
export const ukText = {
  brandName: "Кастомні Меблі",
  tagline: "Під ключ",
  navServices: "Послуги",
  navPortfolio: "Портфоліо",
  navAbout: "Про нас",
  navContact: "Контакти",
  heroTitle: "Створюємо затишок за вашими розмірами",
  heroSubtitle: "Індивідуальні меблі преміум-якості для вашого дому",
  heroBtn1: "Замовити замір",
  heroBtn2: "Наші роботи",
  aboutTitle: "Про нас",
  aboutSubtitle: "Команда професіоналів",
  aboutText: "Ми створюємо якісні меблі під ключ. Сучасний дизайн та індивідуальний підхід.",
  advExperience: "Досвід",
  advExperienceDesc: "Багаторічний досвід роботи",
  advQuality: "Якість",
  advQualityDesc: "Лише перевірені матеріали",
  advWarranty: "Гарантія",
  advWarrantyDesc: "Гарантія на всю продукцію",
  advIndividual: "Індивідуальність",
  advIndividualDesc: "Кожен проєкт унікальний",
  howWeWorkTitle: "Як ми працюємо",
  howWeWorkSubtitle: "Простий процес співпраці",
  step1: "Консультація",
  step1Desc: "Обговорюємо побажання",
  step2: "Замір",
  step2Desc: "Безкоштовний виїзд",
  step3: "Проєкт",
  step3Desc: "3D-візуалізація",
  step4: "Виготовлення",
  step4Desc: "Виробництво меблів",
  step5: "Монтаж",
  step5Desc: "Доставка та монтаж",
  servicesTitle: "Наші послуги",
  servicesSubtitle: "Меблі будь-якої складності",
  learnMore: "Детальніше",
  portfolioTitle: "Портфоліо",
  portfolioSubtitle: "Наші роботи",
  reviewsTitle: "Відгуки",
  reviewsSubtitle: "Що кажуть клієнти",
  faqTitle: "Питання",
  faqSubtitle: "Часті питання",
  contactFormTitle: "Замовити замір",
  contactFormSubtitle: "Залиште заявку",
  formName: "Ваше ім'я",
  formPhone: "Телефон",
  formMessage: "Повідомлення",
  formSubmit: "Відправити",
  formSuccess: "Дякуємо! Ми зв'яжемося з вами",
  footerDesc: "Виготовлення меблів на замовлення",
  contactsTitle: "Контакти",
  addressTitle: "Адреса",
  addressText: "м. Звягель, Житомирська обл.",
  workArea: "Працюємо по Звягелю та району",
  socialTitle: "Соцмережі",
  workHours: "Пн-Сб: 9:00 - 18:00",
  copyright: "Всі права захищено",
  kitchens: "Кухні",
  kitchensDesc: "Функціональні та стильні кухні під замовлення",
  wardrobes: "Шафи-купе",
  wardrobesDesc: "Місткі шафи з розсувними дверима",
  hallways: "Передпокої",
  hallwaysDesc: "Компактні меблі для передпокою",
  faq1q: "Який термін виготовлення?",
  faq1a: "2-4 тижні залежно від складності",
  faq2q: "Чи є гарантія?",
  faq2a: "Так, 2 роки на всю продукцію",
  faq3q: "Які матеріали?",
  faq3a: "ДСП, МДФ, масив, фурнітура Blum, Hettich",
  faq4q: "Доставка входить у вартість?",
  faq4a: "По Звягелю - безкоштовно",
  statProjects: "Проєктів",
  statYears: "Років",
  statClients: "Клієнтів"
};

export const services = [
  { id: 1, title: ukText.kitchens, description: ukText.kitchensDesc, icon: "??" },
  { id: 2, title: ukText.wardrobes, description: ukText.wardrobesDesc, icon: "??" },
  { id: 3, title: ukText.hallways, description: ukText.hallwaysDesc, icon: "??" }
];

export const advantages = [
  { id: 1, icon: "??", title: ukText.advExperience, desc: ukText.advExperienceDesc },
  { id: 2, icon: "?", title: ukText.advQuality, desc: ukText.advQualityDesc },
  { id: 3, icon: "???", title: ukText.advWarranty, desc: ukText.advWarrantyDesc },
  { id: 4, icon: "??", title: ukText.advIndividual, desc: ukText.advIndividualDesc }
];

export const steps = [
  { id: 1, title: ukText.step1, desc: ukText.step1Desc, icon: "??" },
  { id: 2, title: ukText.step2, desc: ukText.step2Desc, icon: "??" },
  { id: 3, title: ukText.step3, desc: ukText.step3Desc, icon: "??" },
  { id: 4, title: ukText.step4, desc: ukText.step4Desc, icon: "??" },
  { id: 5, title: ukText.step5, desc: ukText.step5Desc, icon: "?" }
];

export const faqItems = [
  { id: 1, question: ukText.faq1q, answer: ukText.faq1a },
  { id: 2, question: ukText.faq2q, answer: ukText.faq2a },
  { id: 3, question: ukText.faq3q, answer: ukText.faq3a },
  { id: 4, question: ukText.faq4q, answer: ukText.faq4a }
];

export const categories = [ukText.kitchens, ukText.wardrobes, ukText.hallways];
